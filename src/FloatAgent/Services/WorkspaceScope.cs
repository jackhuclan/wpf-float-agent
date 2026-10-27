using System.IO;
using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>
/// 工作区路径白名单：文件工具只允许读写白名单根内的文件。
/// 根 = exe 目录（主根，相对路径的基准）+ config.json 的 WorkspaceRoots（每次校验动态读取，改配置即生效）。
/// 另设拒绝名单：config.json 含 API Key，读写都拒绝，防止凭据进入对话上下文。
/// </summary>
public sealed class WorkspaceScope
{
    /// <summary>受保护的敏感文件名（任意目录层级、任意大小写均拒绝）。</summary>
    private const string DeniedFileName = "config.json";

    private readonly string _baseRoot;
    private readonly IConfigService _config;

    public WorkspaceScope(IConfigService config, string baseRoot)
    {
        _config = config;
        _baseRoot = NormalizeRoot(baseRoot);
    }

    /// <summary>允许的工作区根目录（主根 + 配置的附加根），用于提示与测试。</summary>
    public IReadOnlyList<string> GetRoots()
    {
        var roots = new List<string> { _baseRoot };
        roots.AddRange(GetExtraRoots());
        return roots;
    }

    /// <summary>
    /// 把用户/模型给的路径解析并校验到白名单内。
    /// 成功时 <paramref name="fullPath"/> 为归一后的绝对路径；失败时 <paramref name="error"/> 给出可读原因。
    /// </summary>
    public bool TryResolve(string? path, out string fullPath, out string? error)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "路径为空。";
            return false;
        }

        try
        {
            var candidate = path.Trim();
            var full = Path.IsPathRooted(candidate)
                ? Path.GetFullPath(candidate)
                : Path.GetFullPath(Path.Combine(_baseRoot, candidate));

            // 必须落在某个白名单根内（含目录分隔符边界检查，堵住 ".." 穿越与同前缀目录名）
            if (!GetRoots().Any(root => IsUnderRoot(full, root)))
            {
                error = $"路径不在允许的工作区内：{full}（允许的根：{string.Join("；", GetRoots())}）。";
                return false;
            }

            if (Path.GetFileName(full).Equals(DeniedFileName, StringComparison.OrdinalIgnoreCase))
            {
                error = $"{DeniedFileName} 是受保护的敏感配置文件（含 API Key），禁止读写。";
                return false;
            }

            fullPath = full;
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = $"路径无效：{ex.Message}";
            return false;
        }
    }

    private IEnumerable<string> GetExtraRoots() =>
        (_config.Current.WorkspaceRoots ?? [])
            .Where(static r => !string.IsNullOrWhiteSpace(r))
            .Select(NormalizeRoot)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeRoot(string root) =>
        Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>full 必须严格位于 root 目录之下（root 本身不算，边界必须是目录分隔符）。</summary>
    private static bool IsUnderRoot(string full, string root) =>
        full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
        full.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
