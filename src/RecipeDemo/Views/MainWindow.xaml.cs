using System.Windows;
using System.Windows.Controls;
using RecipeDemo.Models;
using RecipeDemo.ViewModels;

namespace RecipeDemo.Views;

/// <summary>
/// RecipeDemo 主窗：左侧配方树（搜索/折叠）+ 右侧五个功能标签页。
/// 交互尽量薄：树选中变化转交给 MainViewModel 处理。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>TreeView 不支持 SelectedItem 双向绑定，用事件把选中节点转给 ViewModel。</summary>
    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is RecipeNode node)
        {
            viewModel.SelectedNode = node;
        }
    }
}
