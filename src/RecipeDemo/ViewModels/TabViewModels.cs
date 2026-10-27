using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatAgent.Abstractions;

namespace RecipeDemo.ViewModels;

/// <summary>
/// 右侧功能视图的标签页：Header 显示在顶部标签条，ViewModel 由隐式 DataTemplate 映射到对应 UserControl。
/// </summary>
public abstract partial class TabViewModel : ObservableObject
{
    public string Header { get; }

    /// <summary>标签页对应的视图模型（作为 ContentControl 的 Content）。</summary>
    public TabViewModel ViewModel => this;

    protected TabViewModel(string header) => Header = header;
}

/// <summary>Summary 视图：展示当前选中配方与概要信息；点击左侧配方树后更新。</summary>
public partial class SummaryViewModel : TabViewModel
{
    [ObservableProperty]
    private string recipeName = "—";

    /// <summary>需求点：右侧 Summary 显示「当前选择的配方是：***」。</summary>
    [ObservableProperty]
    private string message = "当前选择的配方是：—";

    [ObservableProperty]
    private string waferCount = "25";

    [ObservableProperty]
    private string lastRunTime = "2026-10-26 18:42";

    [ObservableProperty]
    private string status = "Idle";

    public SummaryViewModel() : base("Summary")
    {
    }

    /// <summary>选中配方变化时更新概要文案。</summary>
    public void SetRecipe(string name)
    {
        RecipeName = name;
        Message = $"当前选择的配方是：{name}";
        Status = "Ready";
    }
}

/// <summary>Wafer Map 视图：初始只有一个「Create Wafer Map」按钮，点击后展示晶圆映射示意。</summary>
public partial class WaferMapViewModel : TabViewModel
{
    /// <summary>是否已创建晶圆图（点击按钮后为 true）。</summary>
    [ObservableProperty]
    private bool isMapVisible;

    public WaferMapViewModel() : base("Wafer Map")
    {
    }

    /// <summary>点击「Create Wafer Map」按钮：生成晶圆图。</summary>
    [RelayCommand]
    private void CreateWaferMap() => IsMapVisible = true;
}

/// <summary>
/// Alignment 视图：Edge / Coarse / Fine 三个对准子页 + 右下角 Save。
/// AI 工具（拉平技能）注册与实现见 AlignmentViewModel.Tools.cs。
/// </summary>
public partial class AlignmentViewModel : TabViewModel
{
    /// <summary>当前配方（由 MainViewModel 在选择配方时同步，save_recipe 用）。</summary>
    public string CurrentRecipeName { get; private set; } = "—";

    /// <summary>操作反馈日志（demo：每次操作追加一行）。</summary>
    [ObservableProperty]
    private string log = "点击上方按钮查看操作反馈。";

    /// <summary>当前子页：0 = Edge，1 = Coarse，2 = Fine（双向绑定 TabControl.SelectedIndex）。</summary>
    [ObservableProperty]
    private int selectedSubTabIndex;

    /// <summary>Edge 页找到的圆心（null 表示尚未找到）。</summary>
    [ObservableProperty]
    private string? waferCenter;

    /// <summary>Coarse / Fine 各自的配方模版是否已创建。</summary>
    [ObservableProperty]
    private bool hasCoarseTemplate;

    [ObservableProperty]
    private bool hasFineTemplate;

    /// <summary>粗定位 / 精定位是否已成功。</summary>
    [ObservableProperty]
    private bool hasCoarseAligned;

    [ObservableProperty]
    private bool hasFineAligned;

    /// <summary>Updating LLC 是否已完成。</summary>
    [ObservableProperty]
    private bool hasLlcDefined;

    private readonly IAIToolService _toolService;

    public AlignmentViewModel(IAIToolService toolService) : base("Alignment")
    {
        _toolService = toolService;
        RegisterTools();
    }

    /// <summary>需要把 Alignment 标签页切到前台时触发（切换子视图的工具用）。</summary>
    internal Action? ActivateRequested { get; set; }

    /// <summary>配方切换时由 MainViewModel 调用：重置对准状态。</summary>
    public void SetRecipe(string name)
    {
        CurrentRecipeName = name;
        WaferCenter = null;
        HasCoarseTemplate = false;
        HasFineTemplate = false;
        HasCoarseAligned = false;
        HasFineAligned = false;
        HasLlcDefined = false;
        ResetLog();
    }

    /// <summary>Edge Alignment 页的 Caculate 按钮。</summary>
    [RelayCommand]
    private void Caculate() => AppendLog(CalculateWaferCenter());

    [RelayCommand]
    private void CreateCoarseTemplate() => AppendLog(CreateTemplate());

    [RelayCommand]
    private void RunCoarse() => AppendLog(RunCoarseAlign());

    [RelayCommand]
    private void CreateFineTemplate() => AppendLog(CreateTemplate());

    [RelayCommand]
    private void RunFine() => AppendLog(RunFineAlign());

    /// <summary>右下角 Save 按钮：保存对准参数。</summary>
    [RelayCommand]
    private void Save() => AppendLog(SaveRecipe());

    private void AppendLog(string message) =>
        Log = Log is "点击上方按钮查看操作反馈。" or "" ? message : $"{Log}{Environment.NewLine}{message}";

    private void ResetLog() => Log = "点击上方按钮查看操作反馈。";
}

/// <summary>单条测试项。</summary>
public record TestItem(string Name, string Result, string Duration);

/// <summary>Tests 视图：测试项列表（demo）。</summary>
public partial class TestsViewModel : TabViewModel
{
    public IReadOnlyList<TestItem> Items { get; } =
    [
        new TestItem("T0001", "Pass", "1.2 s"),
        new TestItem("T0008", "Pass", "0.8 s"),
        new TestItem("Alignment Check", "Pass", "2.5 s"),
        new TestItem("Focus Sweep", "Fail", "3.1 s"),
        new TestItem("Defect Review", "Pending", "—"),
    ];

    public TestsViewModel() : base("Tests")
    {
    }
}

/// <summary>单条运行参数。</summary>
public record RunParameter(string Key, string Value);

/// <summary>Recipe-Run Parameters 视图：运行参数键值对（demo）。</summary>
public partial class RecipeRunParametersViewModel : TabViewModel
{
    public IReadOnlyList<RunParameter> Parameters { get; } =
    [
        new RunParameter("Exposure Time", "120 ms"),
        new RunParameter("Exposure Energy", "35 mJ/cm²"),
        new RunParameter("Focus Offset", "-0.5 μm"),
        new RunParameter("Wafer Count", "25"),
        new RunParameter("Track Temperature", "23.0 ℃"),
        new RunParameter("Chuck Vacuum", "On"),
    ];

    public RecipeRunParametersViewModel() : base("Recipe-Run Parameters")
    {
    }
}
