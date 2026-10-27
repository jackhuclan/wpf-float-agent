using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDemo.Models;

namespace RecipeDemo.ViewModels;

/// <summary>
/// 主窗视图模型：左侧配方树（搜索过滤 / 折叠）+ 右侧五个标签页视图切换。
/// 点击左侧配方树中的配方后，Summary 视图显示「当前选择的配方是：***」。
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const double PanelOpenWidth = 240;
    private const double PanelClosedWidth = 0;

    /// <summary>TreeView 根（仅一个"Recipes"节点）。</summary>
    public ObservableCollection<RecipeNode> RootNodes { get; } = [];

    [ObservableProperty]
    private RecipeNode? selectedNode;

    [ObservableProperty]
    private string searchText = string.Empty;

    /// <summary>左侧配方面板是否展开（小蓝条按钮切换）。</summary>
    [ObservableProperty]
    private bool isPanelOpen = true;

    /// <summary>左侧面板当前宽度（随 IsPanelOpen 变化，绑定到 Border.Width）。</summary>
    [ObservableProperty]
    private double panelWidth = PanelOpenWidth;

    /// <summary>顶部标题：当前选中配方的名字。</summary>
    [ObservableProperty]
    private string selectedRecipeName = string.Empty;

    /// <summary>顶部标签页集合（Summary / Wafer Map / Alignment / Tests / Recipe-Run Parameters）。</summary>
    public ObservableCollection<TabViewModel> Tabs { get; } =
    [
        new SummaryViewModel(),
        new WaferMapViewModel(),
        new AlignmentViewModel(),
        new TestsViewModel(),
        new RecipeRunParametersViewModel(),
    ];

    [ObservableProperty]
    private TabViewModel? selectedTab;

    public SummaryViewModel Summary => (SummaryViewModel)Tabs[0];

    public MainViewModel()
    {
        var root = new RecipeNode("Recipes", isFolder: true, children: BuildDemoTree())
        {
            IsRoot = true,
            IsExpanded = true,
        };
        RootNodes.Add(root);
        SelectedTab = Tabs[0];
    }

    /// <summary>对照参考图构造演示配方树。</summary>
    private static IEnumerable<RecipeNode> BuildDemoTree() =>
    [
        new RecipeNode("abc"),
        new RecipeNode("ddd"),
        new RecipeNode("test"),
        new RecipeNode("张三", isFolder: true, children: [new RecipeNode("T0001"), new RecipeNode("T008")])
        {
            IsExpanded = true,
        },
        new RecipeNode("aa"),
        new RecipeNode("ab"),
        new RecipeNode("ac"),
        new RecipeNode("tet"),
        new RecipeNode("tt"),
        new RecipeNode("tta"),
        new RecipeNode("t44"),
        new RecipeNode("t5"),
    ];

    partial void OnIsPanelOpenChanged(bool value) => PanelWidth = value ? PanelOpenWidth : PanelClosedWidth;

    /// <summary>选中节点变化：配方（叶子）→ 更新标题 + Summary 文案并切到 Summary 页。</summary>
    partial void OnSelectedNodeChanged(RecipeNode? value)
    {
        if (value is null || value.IsFolder)
        {
            return;
        }

        SelectRecipe(value.Name);
    }

    /// <summary>需求点 4：点击配方树后，右侧 Summary 显示当前选择的配方。</summary>
    [RelayCommand]
    private void SelectRecipe(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        SelectedRecipeName = name;
        Summary.SetRecipe(name);
        SelectedTab = Tabs[0];   // 切到 Summary
    }

    /// <summary>Recipes 根旁的 Collapse 按钮：折叠全部节点。</summary>
    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var node in EnumerateAll(RootNodes))
        {
            node.IsExpanded = false;
        }
    }

    /// <summary>左侧面板收起 / 展开（右缘小蓝条）。</summary>
    [RelayCommand]
    private void TogglePanel() => IsPanelOpen = !IsPanelOpen;

    /// <summary>搜索框关键字变化：过滤配方树（保留原节点实例，只重建父子关系）。</summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter(value);

    private void ApplyFilter(string keyword)
    {
        if (RootNodes.Count == 0)
        {
            return;
        }

        var root = RootNodes[0];

        // 深度优先：文件夹自身匹配则整枝保留，否则保留"有可见后代"的文件夹
        RecipeNode? Filter(RecipeNode node)
        {
            if (!node.IsFolder)
            {
                return node.Matches(keyword) ? node : null;
            }

            var visibleChildren = node.AllChildren
                .Select(Filter)
                .Where(static child => child is not null)
                .Cast<RecipeNode>()
                .ToList();

            if (node.Matches(keyword))
            {
                // 关键字命中文件夹本身：整枝展示，恢复原始子节点
                node.Children = [.. node.AllChildren];
                node.IsExpanded = true;
                return node;
            }

            if (visibleChildren.Count == 0)
            {
                return null;
            }

            node.Children = [.. visibleChildren];
            node.IsExpanded = true;
            return node;
        }

        var visibleRoots = root.AllChildren
            .Select(Filter)
            .Where(static node => node is not null)
            .Cast<RecipeNode>()
            .ToList();

        root.Children = [.. visibleRoots];
        root.IsExpanded = true;
    }

    private static IEnumerable<RecipeNode> EnumerateAll(IEnumerable<RecipeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            foreach (var child in EnumerateAll(node.Children))
            {
                yield return child;
            }
        }
    }
}
