using RecipeDemo.Helpers;

namespace RecipeDemo.ViewModels;

/// <summary>
/// Alignment 视图的 AI 工具（对应 skills/alignment 技能的工具契约）：
/// switch_to_edge_alignment / calculate_wafer_center / switch_to_coarse_alignment /
/// create_recipe_template / run_coarse_align / switch_to_fine_alignment / run_fine_align /
/// define_llc / save_recipe。
/// 演示环境没有真实硬件，计算结果为模拟值；失败分支（视图不对、模版未创建）按真实规则拦截。
/// </summary>
public partial class AlignmentViewModel
{
    private const int EdgeTab = 0;
    private const int CoarseTab = 1;
    private const int FineTab = 2;

    private void RegisterTools()
    {
        _toolService.AddTool(
            (Func<string>)ToolSwitchToEdgeAlignment,
            "switch_to_edge_alignment",
            "切换到 Alignment 界面的 Edge Alignment 子视图（圆心定位视角）。");

        _toolService.AddTool(
            (Func<string>)ToolCalculateWaferCenter,
            "calculate_wafer_center",
            "在 Edge 视图自动寻找晶圆圆心，成功后自动把载台移动到圆心位置。（演示环境，返回模拟坐标）");

        _toolService.AddTool(
            (Func<string>)ToolSwitchToCoarseAlignment,
            "switch_to_coarse_alignment",
            "切换到 Alignment 界面的 Coarse Alignment 子视图（粗定位）。");

        _toolService.AddTool(
            (Func<string>)ToolCreateRecipeTemplate,
            "create_recipe_template",
            "在 Coarse 或 Fine 子视图创建配方模版（按当前所在子视图创建对应模版）。");

        _toolService.AddTool(
            (Func<string>)ToolRunCoarseAlign,
            "run_coarse_align",
            "在 Coarse 视图执行粗定位。需已创建配方模版，返回成功 / 取消 / 失败结果。");

        _toolService.AddTool(
            (Func<string>)ToolSwitchToFineAlignment,
            "switch_to_fine_alignment",
            "切换到 Alignment 界面的 Fine Alignment 子视图（精定位）。");

        _toolService.AddTool(
            (Func<string>)ToolRunFineAlign,
            "run_fine_align",
            "在 Fine 视图执行精细拉平。需已创建配方模版，返回成功 / 取消 / 失败结果。");

        _toolService.AddTool(
            (Func<string>)ToolDefineLlc,
            "define_llc",
            "Updating LLC：按当前轴位置计算 WCS 坐标、重绘 WaferMap 并把载台移动到 Field0 LLC。必须先经人工确认 LLC 后调用。");

        _toolService.AddTool(
            (Func<string>)ToolSaveRecipe,
            "save_recipe",
            "保存当前配方的对准结果，返回真实的成功 / 失败结果。");
    }

    /// <summary>工具统一入口：切到 UI 线程执行操作、写操作日志，再把结果交回模型。</summary>
    private string ExecTool(Func<string> operation) =>
        AppHelper.DispatcherInvoke(() =>
        {
            var result = operation();
            AppendLog(result);
            return result;
        });

    private string ToolSwitchToEdgeAlignment() => ExecTool(() => SwitchSubTab(EdgeTab, "Edge Alignment"));

    private string ToolSwitchToCoarseAlignment() => ExecTool(() => SwitchSubTab(CoarseTab, "Coarse Alignment"));

    private string ToolSwitchToFineAlignment() => ExecTool(() => SwitchSubTab(FineTab, "Fine Alignment"));

    private string ToolCalculateWaferCenter() => ExecTool(CalculateWaferCenter);

    private string ToolCreateRecipeTemplate() => ExecTool(CreateTemplate);

    private string ToolRunCoarseAlign() => ExecTool(RunCoarseAlign);

    private string ToolRunFineAlign() => ExecTool(RunFineAlign);

    private string ToolDefineLlc() => ExecTool(DefineLlc);

    private string ToolSaveRecipe() => ExecTool(SaveRecipe);

    /// <summary>切换 Alignment 子视图，并把 Alignment 标签页提到前台。</summary>
    private string SwitchSubTab(int index, string viewName)
    {
        SelectedSubTabIndex = index;
        ActivateRequested?.Invoke();
        return $"已切换到 {viewName} 视图。";
    }

    /// <summary>Edge 视图：自动寻找晶圆圆心（成功后载台移动到圆心）。</summary>
    private string CalculateWaferCenter()
    {
        if (SelectedSubTabIndex != EdgeTab)
        {
            return "自动寻找圆心没有成功：当前不在 Edge Alignment 视图，请先切换到 Edge 视图。";
        }

        WaferCenter = "X=+12.34 μm，Y=-5.67 μm";
        return $"成功找到圆心（{WaferCenter}），载台已移动到圆心位置。";
    }

    /// <summary>Coarse / Fine 视图：创建配方模版。</summary>
    private string CreateTemplate()
    {
        switch (SelectedSubTabIndex)
        {
            case CoarseTab:
                HasCoarseTemplate = true;
                return "配方模版创建成功（Coarse Alignment）。";

            case FineTab:
                HasFineTemplate = true;
                return "配方模版创建成功（Fine Alignment）。";

            default:
                return "创建配方模版失败：当前是 Edge Alignment 视图，请先切换到 Coarse 或 Fine 视图。";
        }
    }

    /// <summary>Coarse 视图：执行粗定位。</summary>
    private string RunCoarseAlign()
    {
        if (SelectedSubTabIndex != CoarseTab)
        {
            return "执行拉平失败：当前不在 Coarse Alignment 视图，请先切换到粗定位视图。";
        }

        if (!HasCoarseTemplate)
        {
            return "执行拉平失败：尚未创建配方模版，请先调用 create_recipe_template。";
        }

        HasCoarseAligned = true;
        return "成功执行粗定位：X 偏移 +2.15 μm，Y 偏移 -0.83 μm，θ 0.0040°。";
    }

    /// <summary>Fine 视图：执行精细拉平。</summary>
    private string RunFineAlign()
    {
        if (SelectedSubTabIndex != FineTab)
        {
            return "执行拉平失败：当前不在 Fine Alignment 视图，请先切换到精定位视图。";
        }

        if (!HasFineTemplate)
        {
            return "执行拉平失败：尚未创建配方模版，请先调用 create_recipe_template。";
        }

        HasFineAligned = true;
        return "成功执行精细拉平：X 偏移 +0.12 μm，Y 偏移 -0.05 μm，θ 0.0002°。";
    }

    /// <summary>精定位后的人工确认卡点：Updating LLC（重绘 WaferMap、载台移到 Field0 LLC）。</summary>
    private string DefineLlc()
    {
        if (!HasFineAligned)
        {
            return "Updating LLC 失败：精定位尚未完成，请先执行 run_fine_align。";
        }

        HasLlcDefined = true;
        return "Updating LLC 成功：WCS 坐标 (128.456 mm, 96.782 mm)，WaferMap 已重绘，载台已移动到 Field0 LLC。";
    }

    /// <summary>右下角 Save：保存当前配方的对准结果。</summary>
    private string SaveRecipe()
    {
        if (CurrentRecipeName is "—" or "")
        {
            return "保存配方失败：当前没有打开的配方，请先选择配方。";
        }

        return $"保存配方成功：配方 {CurrentRecipeName} 的对准数据已保存。";
    }
}
