using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using FloatAgent.Abstractions;
using Microsoft.Agents.AI.Tools.Shell;

namespace Floaticon.Tools;

/// <summary>容器内执行的代码语言。</summary>
public enum ContainerLanguage
{
    CSharp,
    Python,
    JavaScript,
}

/// <summary>
/// Linux 容器代码执行工具：基于
/// <see cref="Microsoft.Agents.AI.Tools.Shell.DockerShellExecutor"/> 在一次性 Linux 容器中
/// 执行 C# / Python / JavaScript 代码，宿主机不需要安装任何运行时。
/// </summary>
/// <remarks>
/// 脚本在构造函数传入，两种形式均可：
/// <list type="bullet">
/// <item>脚本<b>路径</b>（相对/绝对路径，读取文件内容并按扩展名识别语言）；</item>
/// <item>代码<b>文本</b>（按 <paramref name="language"/> 参数或内容特征识别语言）。</item>
/// </list>
/// 代码以 base64 写入容器文件系统后执行，天然规避引号/换行/特殊字符问题；
/// 每次执行创建一次性容器（网络隔离、非 root、1 GiB 内存上限），执行完即销毁。
/// 语言与镜像的对应关系：C# → dotnet/sdk:10.0（容器内生成离线可 restore 的迷你
/// console 工程执行），Python → python:3.12-slim，JavaScript → node:22-slim。
/// </remarks>
public sealed class ContainerCodeTool
{
    private static readonly Dictionary<ContainerLanguage, string> Images = new()
    {
        [ContainerLanguage.CSharp] = "mcr.microsoft.com/dotnet/sdk:10.0",
        [ContainerLanguage.Python] = "docker.io/library/python:3.12-slim",
        [ContainerLanguage.JavaScript] = "docker.io/library/node:22-slim",
    };

    private static readonly Dictionary<ContainerLanguage, string> TempFileNames = new()
    {
        // C# 用迷你工程（/tmp/app/Program.cs + app.csproj）而非文件级应用：
        // dotnet run file.cs 的隐式 restore 必须联网取 NuGet 索引，容器网络隔离时必挂；
        // 普通 console 工程 restore 只用 SDK 自带 targeting pack，可完全离线。
        [ContainerLanguage.CSharp] = "/tmp/app/Program.cs",
        [ContainerLanguage.Python] = "/tmp/floaticon_app.py",
        [ContainerLanguage.JavaScript] = "/tmp/floaticon_app.js",
    };

    private static readonly Dictionary<ContainerLanguage, string> RunCommands = new()
    {
        // C#：显式 build 后用 dotnet 宿主直接加载 dll——容器内非 root 用户对 apphost
        // 原生二进制常有 exec 权限问题（Permission denied），加载 dll 可绕开；
        // 不用 exec（shell 退出码会被执行器读成 -1）。
        [ContainerLanguage.CSharp] = "cd /tmp/app && dotnet build -v q >/dev/null && dotnet bin/Debug/net10.0/app.dll",
        [ContainerLanguage.Python] = "cd /tmp && python3 floaticon_app.py",
        [ContainerLanguage.JavaScript] = "cd /tmp && node floaticon_app.js",
    };

    /// <summary>C# 迷你工程文件：极简 console 工程，restore 不访问网络。</summary>
    private const string CSharpProjectFile =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <NuGetAudit>false</NuGetAudit>
          </PropertyGroup>
        </Project>
        """;

    private readonly ToolProgressService? _progress;
    private readonly string _source;
    private readonly ContainerLanguage? _language;
    private readonly TimeSpan _timeout;

    /// <summary>创建容器代码执行工具。</summary>
    /// <param name="progress">进度广播服务（可选）：开始/周期/完成时上报执行状态。</param>
    /// <param name="source">脚本来源：文件路径或代码文本，运行时自动区分。</param>
    /// <param name="language">
    /// 语言提示（csharp/cs、python/py、javascript/js/node，大小写不敏感）。
    /// 传入路径时默认按扩展名识别；传代码文本时默认按内容特征识别。
    /// </param>
    /// <param name="timeoutSeconds">容器内执行超时（秒），默认 180。首次运行含镜像拉取可能更慢。</param>
    public ContainerCodeTool(
        ToolProgressService? progress,
        string source,
        string? language = null,
        int? timeoutSeconds = null)
    {
        _progress = progress;
        _source = string.IsNullOrWhiteSpace(source)
            ? throw new ArgumentException("脚本来源不能为空。", nameof(source))
            : source;
        _language = ParseLanguage(language);
        _timeout = TimeSpan.FromSeconds(timeoutSeconds ?? 180);
    }

    /// <summary>在 Linux 容器中执行构造函数传入的脚本，返回结构化执行结果。</summary>
    [Description("在 Linux 容器中执行脚本并返回标准输出、标准错误与退出码。")]
    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        // 1) 解析脚本：路径 → 读文件；文本 → 直接使用
        string code;
        string sourceDescription;
        var language = _language;
        if (File.Exists(_source))
        {
            code = await File.ReadAllTextAsync(_source, cancellationToken).ConfigureAwait(false);
            sourceDescription = $"脚本文件 {_source}";
            language ??= LanguageFromExtension(_source)
                         ?? throw new InvalidOperationException(
                             $"无法从扩展名识别脚本语言：{_source}，请在构造时显式指定 language。");
        }
        else
        {
            code = _source;
            sourceDescription = "内联代码";
            language ??= LanguageFromContent(code)
                         ?? throw new InvalidOperationException(
                             "无法从内容特征识别脚本语言，请在构造时显式指定 language。");
        }

        var startedAt = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();
        _progress?.Report($"⚡ {language} 容器任务开始执行（{sourceDescription}）...");

        // 2) 组装容器命令：base64 写入目标文件，规避引号/换行/特殊字符；
        // C# 额外生成迷你工程文件（app.csproj），保证离线 restore 可用
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(code));
        var command = $"printf '%s' '{encoded}' | base64 -d > {TempFileNames[language.Value]} && {RunCommands[language.Value]}";
        if (language == ContainerLanguage.CSharp)
        {
            var projectEncoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(CSharpProjectFile));
            command =
                "mkdir -p /tmp/app" +
                $" && printf '%s' '{projectEncoded}' | base64 -d > /tmp/app/app.csproj" +
                $" && printf '%s' '{encoded}' | base64 -d > {TempFileNames[language.Value]}" +
                $" && {RunCommands[language.Value]}";
        }

        // 3) 一次性容器：网络隔离、可写根文件系统（编译器需要写缓存）、1 GiB 内存上限
        // 非 root 用户没有 HOME，dotnet CLI 需要 DOTNET_CLI_HOME 指向可写目录
        var options = new DockerShellExecutorOptions
        {
            Image = Images[language.Value],
            ReadOnlyRoot = false,
            Network = "none",
            MemoryBytes = 1L << 30,
            Timeout = _timeout,
            Environment = new Dictionary<string, string>
            {
                ["HOME"] = "/tmp",
                ["DOTNET_CLI_HOME"] = "/tmp",
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                // 关闭 workload 联网验证，去掉 stderr 的 "verifying workloads" 噪音
                ["DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"] = "1",
            },
        };

        ShellResult result;
        try
        {
            var executor = new DockerShellExecutor(options);
            await using var _ = executor.ConfigureAwait(false);
            await executor.InitializeAsync(cancellationToken).ConfigureAwait(false);

            // 周期上报：镜像拉取/编译可能耗时数分钟，避免界面零反馈
            var runTask = executor.RunAsync(command, cancellationToken);
            var reporter = ReportPeriodically(runTask, stopwatch, language.Value);
            result = await runTask.ConfigureAwait(false);
            await reporter.ConfigureAwait(false);
        }
        catch (DockerNotAvailableException)
        {
            return "❌ 容器代码执行失败：Docker 不可用。请确认 Docker（或 Podman）已安装并正在运行。";
        }
        catch (ShellCommandRejectedException ex)
        {
            return $"❌ 容器代码执行被安全策略拒绝：{ex.Message}";
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }

        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();
        _progress?.Report($"✅ {language} 容器任务执行完成，耗时 {stopwatch.Elapsed.TotalSeconds:0.0} 秒");

        return FormatResult(language.Value, sourceDescription, result, startedAt, stopwatch.Elapsed);
    }

    /// <summary>解析语言提示文本。</summary>
    private static ContainerLanguage? ParseLanguage(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "csharp" or "cs" or "c#" or "dotnet" => ContainerLanguage.CSharp,
        "python" or "py" or "python3" => ContainerLanguage.Python,
        "javascript" or "js" or "node" or "nodejs" => ContainerLanguage.JavaScript,
        null or "" => null,
        _ => throw new ArgumentException($"不支持的语言：{language}（支持 csharp / python / javascript）。"),
    };

    /// <summary>按脚本扩展名识别语言。</summary>
    private static ContainerLanguage? LanguageFromExtension(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => ContainerLanguage.CSharp,
            ".py" or ".pyw" => ContainerLanguage.Python,
            ".js" or ".mjs" or ".cjs" => ContainerLanguage.JavaScript,
            _ => null,
        };

    /// <summary>按代码内容特征识别语言（无法识别时返回 null，要求调用方显式指定）。</summary>
    private static ContainerLanguage? LanguageFromContent(string code)
    {
        if (code.Contains("Console.Write") || code.Contains("using System") || code.Contains("static void Main"))
        {
            return ContainerLanguage.CSharp;
        }

        if (code.Contains("console.log") || code.Contains("console.error")
            || code.Contains("require(") || code.Contains("export "))
        {
            return ContainerLanguage.JavaScript;
        }

        if (code.Contains("print(") || code.Contains("import ") || code.Contains("def "))
        {
            return ContainerLanguage.Python;
        }

        return null;
    }

    /// <summary>执行期间每 5 秒上报一次进度，任务结束即停。</summary>
    private async Task ReportPeriodically(Task runTask, Stopwatch stopwatch, ContainerLanguage language)
    {
        while (!runTask.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (runTask.IsCompleted)
            {
                break;
            }

            _progress?.Report($"⚡ {language} 容器任务执行中... 已运行 {stopwatch.Elapsed.TotalSeconds:0} 秒" +
                              "（首次运行需拉取镜像，可能较慢）");
        }
    }

    /// <summary>把 ShellResult 组织成给模型的结构化 Markdown。</summary>
    private static string FormatResult(
        ContainerLanguage language, string sourceDescription, ShellResult result,
        DateTime startedAt, TimeSpan elapsed)
    {
        var status = result.ExitCode == 0 ? "成功" : "失败";
        var builder = new StringBuilder();
        builder.AppendLine($"**容器代码执行{status}**");
        builder.AppendLine();
        builder.AppendLine($"- 语言：{language}");
        builder.AppendLine($"- 来源：{sourceDescription}");
        builder.AppendLine($"- 状态：{status}（退出码 {result.ExitCode}）");
        builder.AppendLine($"- 开始时间：{startedAt:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"- 实际耗时：{elapsed.TotalSeconds:0.0} 秒（容器内 {result.Duration.TotalSeconds:0.0} 秒）");
        if (result.TimedOut)
        {
            builder.AppendLine("- ⚠️ 执行超时被终止");
        }

        builder.AppendLine();
        builder.AppendLine("**标准输出：**");
        builder.AppendLine("```text");
        builder.AppendLine(string.IsNullOrWhiteSpace(result.Stdout) ? "（无输出）" : result.Stdout.TrimEnd());
        builder.AppendLine("```");

        if (!string.IsNullOrWhiteSpace(result.Stderr))
        {
            builder.AppendLine("**标准错误：**");
            builder.AppendLine("```text");
            builder.AppendLine(result.Stderr.TrimEnd());
            builder.AppendLine("```");
        }

        return builder.ToString().TrimEnd();
    }
}
