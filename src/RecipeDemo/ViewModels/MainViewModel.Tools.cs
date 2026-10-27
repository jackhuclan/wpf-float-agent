using System.ComponentModel;
using RecipeDemo.Helpers;
using RecipeDemo.Models;

namespace RecipeDemo.ViewModels;

/// <summary>
/// 主窗的 AI 工具（对应 skills/alignment 技能阶段 0、1）：
/// select_recipe 在配方树中查找并打开配方；open_alignment 从配方跳转到拉平界面。
/// </summary>
public partial class MainViewModel
{
    private void RegisterTools()
    {
        _toolService.AddTool(
            (Func<string, string>)SelectRecipeTool,
            "select_recipe",
            "在左侧配方树中按名称查找配方并打开。返回「找到了您需要的配方X」/「找到多个配方…」/「没有找到配方…」。");

        _toolService.AddTool(
            (Func<string>)OpenAlignmentTool,
            "open_alignment",
            "从配方界面跳转到 Alignment（拉平）界面。");
    }

    private string SelectRecipeTool(
        [Description("要打开的配方名称，例如 test、T0001、张三")]
        string recipeName) =>
        AppHelper.DispatcherInvoke(() => SelectRecipeCore(recipeName));

    private string OpenAlignmentTool()
    {
        AppHelper.DispatcherInvoke(() => SelectedTab = Alignment);
        return "已进入拉平界面（Alignment）。";
    }

    /// <summary>在配方树中查找配方：完全匹配优先，其次包含匹配；不做任何猜测。</summary>
    private string SelectRecipeCore(string recipeName)
    {
        var keyword = recipeName?.Trim() ?? string.Empty;
        if (keyword.Length == 0)
        {
            return "没有找到配方：配方名称为空，请提供准确的配方名称。";
        }

        // 搜索框有过滤时先恢复完整树，否则被过滤掉的配方查不到
        if (!string.IsNullOrEmpty(SearchText))
        {
            SearchText = string.Empty;
        }

        var recipes = EnumerateAll(RootNodes)
            .Where(static node => !node.IsFolder)
            .ToList();

        var exact = recipes
            .Where(node => string.Equals(node.Name, keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (exact.Count == 1)
        {
            return OpenRecipe(exact[0]);
        }

        var candidates = recipes
            .Where(node => node.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return candidates.Count switch
        {
            0 => $"没有找到配方{keyword}，请确认配方名称后重试。",
            1 => OpenRecipe(candidates[0]),
            _ => $"找到多个配方：{string.Join("、", candidates.Select(static node => node.Name))}，请指定要使用的配方名称。",
        };
    }

    /// <summary>打开指定配方：展开树、选中节点、Summary 显示当前配方。</summary>
    private string OpenRecipe(RecipeNode node)
    {
        foreach (var folder in EnumerateAll(RootNodes).Where(static n => n.IsFolder))
        {
            folder.IsExpanded = true;
        }

        node.IsSelected = true;
        SelectedRecipeName = node.Name;
        Summary.SetRecipe(node.Name);
        Alignment.SetRecipe(node.Name);
        SelectedTab = Summary;

        return $"找到了您需要的配方{node.Name}，已在界面中打开。";
    }
}
