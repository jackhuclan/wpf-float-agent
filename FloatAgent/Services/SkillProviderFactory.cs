using System.IO;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace Floaticon.Services;

/// <summary>
/// 创建 Agent Skills 提供程序：扫描 skills 目录下的 SKILL.md，
/// Agent 运行时按需 load_skill / read_skill_resource（渐进式披露）。
/// </summary>
public static class SkillProviderFactory
{
    public const string FolderName = "skills";

    /// <summary>优先使用输出目录下的 skills（随 csproj 复制），其次当前工作目录。</summary>
    public static string ResolveSkillsDirectory()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, FolderName),
            Path.Combine(Directory.GetCurrentDirectory(), FolderName),
        ];

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    public static AgentSkillsProvider Create(ILoggerFactory? loggerFactory = null)
        => Create(ResolveSkillsDirectory(), loggerFactory);

    public static AgentSkillsProvider Create(string skillsDirectory, ILoggerFactory? loggerFactory = null)
    {
        Directory.CreateDirectory(skillsDirectory);

        var fileOptions = new AgentFileSkillsSourceOptions
        {
            // skills/<name>/SKILL.md，留一层余量
            SearchDepth = 3,
            // 只暴露文档类资源；本应用不执行脚本
            AllowedResourceExtensions = [".md", ".json", ".yaml", ".yml", ".txt"],
            AllowedScriptExtensions = [],
        };

        var providerOptions = new AgentSkillsProviderOptions
        {
            // 桌面端没有审批 UI，全部自动放行
            DisableLoadSkillApproval = true,
            DisableReadSkillResourceApproval = true,
            DisableRunSkillScriptApproval = true,
        };

        return new AgentSkillsProvider(
            skillsDirectory,
            scriptRunner: null,
            fileOptions: fileOptions,
            options: providerOptions,
            loggerFactory: loggerFactory);
    }
}
