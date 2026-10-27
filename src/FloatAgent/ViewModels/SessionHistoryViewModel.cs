using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Floaticon.Services;

namespace Floaticon.ViewModels;

/// <summary>
/// 会话抽屉 ViewModel（FR-3/FR-4）：历史会话列表 + 新建 / 打开 / 重命名 / 删除。
/// 自身不直接驱动聊天区，经构造回调与 ChatViewModel 协作；抽屉的打开关闭由 ChatWindow 负责。
/// </summary>
public partial class SessionHistoryViewModel : ObservableObject
{
    private readonly ISessionStore _store;
    private readonly Action<string> _openSession;
    private readonly Action _createNew;
    private readonly Action<string> _deleteSession;
    private readonly Action<string, string> _renameSession;

    public ObservableCollection<SessionListItemViewModel> Sessions { get; } = [];

    /// <summary>请求关闭抽屉（打开/新建会话后由 ChatWindow 订阅收起抽屉）。</summary>
    public event Action? DrawerCloseRequested;

    public SessionHistoryViewModel(
        ISessionStore store,
        Action<string> openSession,
        Action createNew,
        Action<string> deleteSession,
        Action<string, string> renameSession)
    {
        _store = store;
        _openSession = openSession;
        _createNew = createNew;
        _deleteSession = deleteSession;
        _renameSession = renameSession;
    }

    /// <summary>重建列表（抽屉打开、会话增删改后调用）。</summary>
    public void Refresh(string currentSessionId)
    {
        Sessions.Clear();

        foreach (var summary in _store.List())
        {
            var item = new SessionListItemViewModel(
                summary.SessionId, summary.Title,
                summary.CreatedAt, summary.UpdatedAt, summary.MessageCount)
            {
                IsCurrent = summary.SessionId == currentSessionId,
                Owner = this,
            };

            Sessions.Add(item);
        }
    }

    [RelayCommand]
    private void NewSession()
    {
        _createNew();
        DrawerCloseRequested?.Invoke();
    }

    /// <summary>「« 收起」：仅请求收起抽屉，不动会话。</summary>
    [RelayCommand]
    private void CloseDrawer() => DrawerCloseRequested?.Invoke();

    [RelayCommand]
    private void OpenSession(SessionListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        _openSession(item.SessionId);
        DrawerCloseRequested?.Invoke();
    }

    // ---- 行内重命名（FR-4.6/4.7：默认全选、Enter 确认、Esc 放弃、失焦视为确认） ----

    [RelayCommand]
    private void StartRename(SessionListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.RenameText = item.Title;
        item.IsRenaming = true;
    }

    [RelayCommand]
    private void CommitRename(SessionListItemViewModel? item)
    {
        if (item is null || !item.IsRenaming)
        {
            return;
        }

        item.IsRenaming = false;
        var title = item.RenameText.Trim();
        if (title.Length == 0 || title == item.Title)
        {
            return;     // 空白输入保持原标题（FR-4.7）
        }

        _store.Rename(item.SessionId, title);
        item.Title = title;
        _renameSession(item.SessionId, title);
    }

    [RelayCommand]
    private void CancelRename(SessionListItemViewModel? item)
    {
        if (item is not null)
        {
            item.IsRenaming = false;
        }
    }

    // ---- 删除（FR-4.5 二次确认，FR-4.9 删除当前会话后自动切换） ----

    [RelayCommand]
    private void RequestDelete(SessionListItemViewModel? item)
    {
        if (item is not null)
        {
            item.IsConfirmingDelete = true;
        }
    }

    [RelayCommand]
    private void ConfirmDelete(SessionListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        item.IsConfirmingDelete = false;
        _deleteSession(item.SessionId);
    }

    [RelayCommand]
    private void CancelDelete(SessionListItemViewModel? item)
    {
        if (item is not null)
        {
            item.IsConfirmingDelete = false;
        }
    }
}
