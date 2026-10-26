using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Floaticon.Models;

namespace Floaticon.ViewModels;

/// <summary>
/// 一条聊天消息。IsUser 决定气泡靠右（蓝）还是靠左（白）。
/// </summary>
public partial class ChatMessage : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isUser;

    /// <summary>是否正在生成中（用于"对方正在输入…"占位）。</summary>
    [ObservableProperty]
    private bool _isTyping;

    /// <summary>
    /// 生成期间的附加状态（如长耗时工具的执行进度），显示在“正在输入”气泡下方；
    /// 无状态时为 null（不占位）。
    /// </summary>
    [ObservableProperty]
    private string? _status;

    /// <summary>本次回复的 token 用量（仅 AI 消息，流结束后回填）。</summary>
    [ObservableProperty]
    private TokenUsage? _usage;

    /// <summary>生成该回复所用的模型名（仅 AI 消息）。</summary>
    [ObservableProperty]
    private string? _modelName;

    /// <summary>消息时间（用户气泡悬浮时显示在复制按钮左侧）。</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// 悬浮行显示的时间文本：今天只显示时刻「11:25」，昨天及以前带日期「10月24日 09:50」。
    /// </summary>
    public string TimeText => Timestamp.Date == DateTime.Today
        ? Timestamp.ToString("HH:mm")
        : Timestamp.ToString("M月d日 HH:mm");

    public bool HasUsage => Usage is not null;

    partial void OnUsageChanged(TokenUsage? value)
    {
        OnPropertyChanged(nameof(HasUsage));
        OnPropertyChanged(nameof(UsageText));
    }

    partial void OnModelNameChanged(string? value) => OnPropertyChanged(nameof(ModelText));

    /// <summary>工具栏显示的用量文本，如「共消耗 1,234 tokens」。</summary>
    public string UsageText => Usage is null ? string.Empty : $"共消耗 {Usage.TotalTokens:N0} tokens";

    /// <summary>工具栏显示的模型文本，如「deepseek-chat」。</summary>
    public string ModelText => ModelName ?? string.Empty;

    public ChatMessage(string text, bool isUser, bool isTyping = false)
    {
        _text = text;
        _isUser = isUser;
        _isTyping = isTyping;
    }

    /// <summary>把原始消息文本复制到剪贴板（气泡右键菜单与 AI 工具栏共用）。</summary>
    [RelayCommand]
    private void Copy()
    {
        if (string.IsNullOrEmpty(Text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(Text);
        }
        catch (Exception)
        {
            // 剪贴板偶尔被其他进程锁定，忽略即可
        }
    }
}
