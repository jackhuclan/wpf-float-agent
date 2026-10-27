using CommunityToolkit.Mvvm.ComponentModel;

namespace RecipeDemo.Models;

/// <summary>
/// 配方树节点：文件夹或配方（叶子）。IsRoot 的节点是顶层"Recipes"根，
/// 头部会额外显示一个 Collapse 按钮。
/// </summary>
public partial class RecipeNode : ObservableObject
{
    public string Name { get; }

    public bool IsFolder { get; }

    /// <summary>是否为顶层"Recipes"根节点（显示 Collapse 按钮）。</summary>
    [ObservableProperty]
    private bool isRoot;

    /// <summary>树节点展开状态（仅文件夹有意义，双向绑定到 TreeViewItem）。</summary>
    [ObservableProperty]
    private bool isExpanded;

    /// <summary>选中状态（双向绑定到 TreeViewItem，配合模板高亮）。</summary>
    [ObservableProperty]
    private bool isSelected;

    /// <summary>过滤前的完整子节点（文件夹用）；叶子为空集合。</summary>
    public IReadOnlyList<RecipeNode> AllChildren { get; } = [];

    /// <summary>当前展示的子节点（搜索过滤后重建，保留原节点实例）。</summary>
    [ObservableProperty]
    private System.Collections.ObjectModel.ObservableCollection<RecipeNode> children = [];

    public RecipeNode(string name, bool isFolder = false, IEnumerable<RecipeNode>? children = null)
    {
        Name = name;
        IsFolder = isFolder;

        if (children is not null)
        {
            AllChildren = [.. children];
            Children = [.. AllChildren];
        }
    }

    /// <summary>按关键字判断自身是否匹配（大小写不敏感；空关键字视为匹配）。</summary>
    public bool Matches(string keyword) =>
        string.IsNullOrWhiteSpace(keyword) || Name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
}
