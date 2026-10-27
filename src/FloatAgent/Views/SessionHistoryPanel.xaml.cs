using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Floaticon.ViewModels;

namespace Floaticon.Views;

/// <summary>
/// 会话抽屉面板：历史会话列表 + 新建 / 重命名 / 删除。
/// DataContext 为 <see cref="SessionHistoryViewModel"/>。
/// </summary>
public partial class SessionHistoryPanel : UserControl
{
    public SessionHistoryPanel()
    {
        InitializeComponent();
    }

    private SessionHistoryViewModel? ViewModel => DataContext as SessionHistoryViewModel;

    /// <summary>行内重命名（FR-4.7）：Enter 确认、Esc 放弃；失焦视为确认（见 LostFocus）。</summary>
    private void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box
            || box.DataContext is not SessionListItemViewModel item
            || ViewModel is not { } vm)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.CommitRenameCommand.Execute(item);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelRenameCommand.Execute(item);
            e.Handled = true;
        }
    }

    /// <summary>重命名框失焦视为确认（FR-4.7）；Esc 放弃后失焦不重复提交。</summary>
    private void OnRenameLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box
            || box.DataContext is not SessionListItemViewModel { IsRenaming: true } item
            || ViewModel is not { } vm)
        {
            return;
        }

        vm.CommitRenameCommand.Execute(item);
    }

    /// <summary>重命名框可见时聚焦并全选原标题（FR-4.6）。</summary>
    private void OnRenameBoxVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox box)
        {
            box.Dispatcher.BeginInvoke(() =>
            {
                box.Focus();
                box.SelectAll();
            });
        }
    }
}
