using CommunityToolkit.Mvvm.ComponentModel;

namespace Floaticon.ViewModels;

/// <summary>
/// 会话抽屉里的一条历史会话项：标题 / 日期副标题（FR-4.2）/ 当前会话高亮，
/// 以及行内重命名（FR-4.6/4.7）与删除二次确认（FR-4.5）的界面态。
/// 命令由 <see cref="SessionHistoryViewModel"/> 统一持有，右键菜单经 <see cref="Owner"/> 转发。
/// </summary>
public partial class SessionListItemViewModel : ObservableObject
{
    public string SessionId { get; }

    [ObservableProperty]
    private string _title;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; }

    public int MessageCount { get; }

    /// <summary>是否为当前打开的会话（列表高亮）。</summary>
    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>行内重命名进行中（标题区切换为文本框）。</summary>
    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _renameText = string.Empty;

    /// <summary>删除二次确认进行中（操作区切换为 确认/取消 按钮）。</summary>
    [ObservableProperty]
    private bool _isConfirmingDelete;

    /// <summary>所属抽屉 VM（右键菜单绑定命令用）。</summary>
    public SessionHistoryViewModel? Owner { get; set; }

    public SessionListItemViewModel(string sessionId, string title, DateTimeOffset createdAt, DateTimeOffset updatedAt, int messageCount)
    {
        SessionId = sessionId;
        _title = title;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        MessageCount = messageCount;
    }

    /// <summary>
    /// 日期副标题（FR-4.2 / B15）：今天/昨天显示「今天 HH:mm / 昨天 HH:mm」，
    /// 同年「M月d日」，跨年「yyyy年M月d日」。
    /// </summary>
    public string DateText
    {
        get
        {
            var local = UpdatedAt.LocalDateTime;
            var today = DateTime.Today;

            return local.Date switch
            {
                var d when d == today => $"今天 {local:HH:mm}",
                var d when d == today.AddDays(-1) => $"昨天 {local:HH:mm}",
                var d when d.Year == today.Year => local.ToString("M月d日"),
                _ => local.ToString("yyyy年M月d日"),
            };
        }
    }

    /// <summary>悬浮提示的完整时间（FR-4.2）。</summary>
    public string FullTime => UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
}
