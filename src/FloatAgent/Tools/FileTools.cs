using System.ComponentModel;
using System.IO;
using System.Text;
using Floaticon.Services;
using Microsoft.Extensions.Logging;

namespace Floaticon.Tools;

/// <summary>
/// 内置文件读写工具：让模型能在受控工作区内读取 / 写入文本文件。
/// 安全边界由 <see cref="WorkspaceScope"/> 把守（白名单根 + config.json 拒绝名单）；
/// write_file 在写入前必须经 <see cref="IConfirmationService"/> 人工确认。
/// 所有失败都以可读字符串返回（不抛异常），便于模型向用户解释原因。
/// </summary>
public class FileTools
{
    /// <summary>单次读取的文件大小上限。</summary>
    private const long MaxReadBytes = 10 * 1024 * 1024;

    /// <summary>二进制探测字节数：前 8KB 出现 NUL 即判为二进制。</summary>
    private const int BinarySniffBytes = 8192;

    /// <summary>单行显示上限，超出截断，防止超长行（如压缩后的 js/json）撑爆上下文。</summary>
    private const int MaxLineDisplayLength = 2000;

    /// <summary>单次读取行数上限（limit 参数超出按此值钳制）。</summary>
    private const int MaxReadLines = 5000;

    /// <summary>单次写入的字符数上限。</summary>
    private const int MaxWriteChars = 2_000_000;

    /// <summary>确认卡里展示的写入内容预览行数。</summary>
    private const int PreviewLines = 15;

    private readonly WorkspaceScope _scope;
    private readonly IConfirmationService _confirmation;
    private readonly ILogger<FileTools> _logger;

    public FileTools(WorkspaceScope scope, IConfirmationService confirmation, ILogger<FileTools> logger)
    {
        _scope = scope;
        _confirmation = confirmation;
        _logger = logger;
    }

    /// <summary>read_file：按行号分页读取白名单内的文本文件。</summary>
    [Description("读取工作区内一个文本文件的内容（每行前带行号）。默认返回前 2000 行，大文件可用 offset/limit 翻页续读；单行超过 2000 字符会被截断；二进制文件与超过 10MB 的文件会被拒绝。只能读取允许的工作区路径（运行目录及配置的 WorkspaceRoots），config.json 等敏感文件被禁止读取。")]
    public async Task<string> ReadFileAsync(
        [Description("文件路径：工作区内的相对路径（相对运行目录）或工作区内的绝对路径")] string path,
        [Description("起始行号（从 1 开始），默认 1")] int offset = 1,
        [Description("本次最多读取的行数，默认 2000，最大 5000")] int limit = 2000,
        CancellationToken cancellationToken = default)
    {
        if (!_scope.TryResolve(path, out var fullPath, out var resolveError))
        {
            return $"读取失败：{resolveError}";
        }

        try
        {
            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                return Directory.Exists(fullPath)
                    ? $"读取失败：{fullPath} 是一个目录，不是文件。"
                    : $"读取失败：文件不存在：{fullPath}";
            }

            if (info.Length > MaxReadBytes)
            {
                return $"读取失败：文件过大（{info.Length:N0} 字节，上限 10 MB）：{fullPath}。请让用户手动处理或改用其他方式。";
            }

            if (await IsBinaryAsync(fullPath, cancellationToken))
            {
                return $"读取失败：{fullPath} 疑似二进制文件，无法按文本读取。";
            }

            var lines = await File.ReadAllLinesAsync(fullPath, cancellationToken);
            if (lines.Length == 0)
            {
                return $"[文件] {fullPath}（空文件，0 行）";
            }

            var start = Math.Max(1, offset);
            var count = limit <= 0 ? 2000 : Math.Min(limit, MaxReadLines);
            var end = Math.Min(lines.Length, start - 1 + count);

            if (start > lines.Length)
            {
                return $"[文件] {fullPath}（共 {lines.Length:N0} 行）——起始行 {start} 超出文件末尾，没有内容可读。";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[文件] {fullPath}（共 {lines.Length:N0} 行，显示第 {start:N0}–{end:N0} 行）");

            for (var i = start; i <= end; i++)
            {
                var line = lines[i - 1];
                if (line.Length > MaxLineDisplayLength)
                {
                    line = $"{line[..MaxLineDisplayLength]}…（本行超长已截断，原始长度 {line.Length:N0} 字符）";
                }

                builder.Append($"{i,6}→").AppendLine(line);
            }

            if (end < lines.Length)
            {
                builder.Append($"（还有 {lines.Length - end:N0} 行未显示，可用 offset={end + 1} 续读）");
            }

            return builder.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[file] read_file 失败：{Path}", fullPath);
            return $"读取失败：{ex.Message}（{fullPath}）";
        }
    }

    /// <summary>write_file：确认通过后整文件覆盖写入（自动创建父目录）。</summary>
    [Description("把文本内容写入工作区内的文件（整文件覆盖；父目录不存在会自动创建；编码 UTF-8 无 BOM）。写入前必须经用户在聊天窗确认，用户拒绝、确认超时（120 秒）或取消时不会写入任何内容。只能写允许的工作区路径（运行目录及配置的 WorkspaceRoots），config.json 等敏感文件被禁止写入。")]
    public async Task<string> WriteFileAsync(
        [Description("目标文件路径：工作区内的相对路径（相对运行目录）或工作区内的绝对路径")] string path,
        [Description("要写入的完整文本内容（覆盖原文件全部内容）")] string? content,
        CancellationToken cancellationToken = default)
    {
        if (!_scope.TryResolve(path, out var fullPath, out var resolveError))
        {
            return $"写入失败：{resolveError}";
        }

        var text = content ?? string.Empty;
        if (text.Length > MaxWriteChars)
        {
            return $"写入失败：内容过大（{text.Length:N0} 字符，上限 {MaxWriteChars:N0}）。请拆分成多次写入或减小内容。";
        }

        try
        {
            var existed = File.Exists(fullPath);
            var approved = await _confirmation.RequestConfirmationAsync(
                "write_file",
                existed ? "覆盖文件" : "新建文件",
                fullPath,
                BuildPreview(text),
                cancellationToken);

            if (!approved)
            {
                _logger.LogInformation("[file] write_file 被用户拒绝或确认超时：{Path}", fullPath);
                return "用户拒绝了本次写入（或确认超时）。未写入任何内容，请不要重试，先向用户说明并确认意图。";
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);

            var lineCount = text.Length == 0 ? 0 : text.Count(static c => c == '\n') + 1;
            var byteCount = Encoding.UTF8.GetByteCount(text);
            var result = $"已写入：{fullPath}（{lineCount:N0} 行 / {byteCount:N0} 字节，{(existed ? "覆盖原文件" : "新建文件")}）";
            _logger.LogInformation("[file] write_file 完成：{Path}（{Bytes} 字节）", fullPath, byteCount);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[file] write_file 失败：{Path}", fullPath);
            return $"写入失败：{ex.Message}（{fullPath}）";
        }
    }

    /// <summary>探测文件是否为二进制：前 8KB 内出现 NUL 字节即判定。</summary>
    private static async Task<bool> IsBinaryAsync(string fullPath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(fullPath);
        var buffer = new byte[BinarySniffBytes];
        var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
        return Array.IndexOf(buffer, (byte)0, 0, read) >= 0;
    }

    /// <summary>确认卡预览：前若干行 + 溢出说明，单行再截一刀。</summary>
    private static string? BuildPreview(string text)
    {
        if (text.Length == 0)
        {
            return "（空内容）";
        }

        var lines = text.Split('\n');
        var shown = lines.Take(PreviewLines)
            .Select(static line => line.Length > 120 ? line[..120] + "…" : line.TrimEnd('\r'));
        var preview = string.Join(Environment.NewLine, shown);

        if (lines.Length > PreviewLines)
        {
            preview += $"{Environment.NewLine}…（共 {lines.Length:N0} 行，仅预览前 {PreviewLines} 行）";
        }

        return preview;
    }
}
