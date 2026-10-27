using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatAgent.Abstractions;
using Floaticon.Models;
using Floaticon.Services;
using Microsoft.Extensions.Logging;

namespace Floaticon.ViewModels;

/// <summary>
/// 聊天窗 ViewModel：消息集合 + 流式发送。
/// 通过 IChatService 调用 DeepSeek，回复逐字追加到气泡中。
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private const string Greeting =
        "你好，我是你的 AI 助手，请问我有什么能帮助你的...";

    private readonly IChatService _chatService;
    private readonly ToolProgressService _progress;
    private readonly ISessionStore _sessionStore;
    private readonly ToolCallLog _toolLog;
    private readonly IConfirmationService _confirmation;
    private readonly ILogger<ChatViewModel> _logger;
    private ChatSession _session;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>当前会话 guid（即会话 json 的文件名）。</summary>
    public string SessionId => _session.SessionId;

    /// <summary>当前会话文件完整路径（标题栏 tooltip 用）。</summary>
    public string SessionFilePath => Path.Combine(_sessionStore.SessionsDirectory, _session.FileName);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    [NotifyCanExecuteChangedFor(nameof(NewSessionCommand))]
    private bool _isBusy;

    public ChatViewModel(IChatService chatService, ToolProgressService progress, ISessionStore sessionStore, ToolCallLog toolLog, IConfirmationService confirmation, ILogger<ChatViewModel> logger)
    {
        _chatService = chatService;
        _progress = progress;
        _sessionStore = sessionStore;
        _toolLog = toolLog;
        _confirmation = confirmation;
        _logger = logger;

        // 写操作确认：工具线程发来请求 → UI 线程插确认卡（见 OnConfirmationRequested）
        _confirmation.Requested += OnConfirmationRequested;

        // 每次启动（或点"新建会话"）都开一次新会话：立即生成 guid 并按该 guid 落盘
        _session = _sessionStore.StartNewSession();
        Messages.Add(new ChatMessage(Greeting, isUser: false));
        Persist();
    }

    /// <summary>开一次新对话：换新 guid、清空消息并立刻写出新的会话文件。</summary>
    private bool CanNewSession() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanNewSession))]
    private void NewSession()
    {
        _cts?.Cancel();

        Messages.Clear();
        _session = _sessionStore.StartNewSession();
        _toolLog.Clear();   // 工具执行日志以会话为边界
        Messages.Add(new ChatMessage(Greeting, isUser: false));

        Persist();
        OnPropertyChanged(nameof(SessionId));
        OnPropertyChanged(nameof(SessionFilePath));
    }

    /// <summary>把界面消息同步到会话对象并写盘（任何失败都不影响聊天）。</summary>
    private void Persist()
    {
        try
        {
            SyncSession();
            _sessionStore.Save(_session);
        }
        catch (Exception ex)
        {
            // 会话保存是旁路能力：磁盘满/权限不足时只记日志，不打断对话
            _logger.LogError(ex, "[session] 会话保存失败（文件：{File}）", _session.FileName);
        }
    }

    /// <summary>把当前消息列表重建为会话内容（跳过"正在输入"占位与空文本）。</summary>
    private void SyncSession()
    {
        _session.Messages.Clear();

        foreach (var message in Messages)
        {
            if (message.IsTyping || string.IsNullOrWhiteSpace(message.Text))
            {
                continue;
            }

            _session.Messages.Add(new SessionMessage
            {
                Role = message.IsUser ? "user" : "assistant",
                Text = message.Text,
                Timestamp = message.Timestamp,
                TotalTokens = message.Usage?.TotalTokens,
                Model = message.ModelName,
            });
        }

        // 标题取第一条用户消息（截 24 字，换行压成空格）
        if (_session.Messages.FirstOrDefault(static m => m.Role == "user") is { } first)
        {
            var title = first.Text.ReplaceLineEndings(" ").Trim();
            _session.Title = title.Length <= 24 ? title : title[..24];
        }
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var userText = InputText.Trim();
        InputText = string.Empty;

        Messages.Add(new ChatMessage(userText, isUser: true));
        Persist();   // 用户消息先落盘，避免中途崩溃丢掉提问

        // 占位气泡：先显示"正在输入"，收到首块内容后切换为正文
        var reply = new ChatMessage(string.Empty, isUser: false, isTyping: true);
        Messages.Add(reply);

        // 取历史（排除占位气泡）作为上下文
        var history = Messages
            .Where(m => !m.IsTyping)
            .Select(m => new ChatTurn(m.IsUser ? "user" : "assistant", m.Text))
            .ToList();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        IsBusy = true;
        var buffer = new StringBuilder();
        var flushWatch = Stopwatch.StartNew();
        TokenUsage? lastUsage = null;

        // 长耗时工具的执行进度：工具在线程池上报 → 调度回 UI 线程 → 打到占位气泡
        void OnProgress(string message)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                reply.Status = message;
                return;
            }

            dispatcher.InvokeAsync(() => reply.Status = message);
        }

        _progress.Progress += OnProgress;
        try
        {
            await foreach (var chunk in _chatService.StreamReplyAsync(history, _cts.Token))
            {
                if (chunk.Text is not null)
                {
                    if (buffer.Length == 0 && reply.Status is not null)
                    {
                        // 首块正文到达，撤掉工具进度提示
                        reply.Status = null;
                    }

                    buffer.Append(chunk.Text);

                    // 限流刷新：Markdown 每更新一次都要整篇重排，按 60ms 一批刷新更顺滑
                    if (flushWatch.ElapsedMilliseconds >= 60)
                    {
                        reply.Text = buffer.ToString();
                        reply.IsTyping = false;
                        flushWatch.Restart();
                    }
                }

                if (chunk.Usage is not null)
                {
                    lastUsage = chunk.Usage;
                }
            }

            reply.Text = buffer.Length == 0 ? "（没有收到内容，请重试）" : buffer.ToString();
            reply.IsTyping = false;

            // 流结束后回填用量与模型名（驱动 AI 气泡下方工具栏显示）
            if (lastUsage is not null)
            {
                reply.Usage = lastUsage;
                reply.ModelName = lastUsage.Model;
            }
        }
        catch (OperationCanceledException)
        {
            reply.Text = buffer.Length > 0
                ? $"{buffer}\n\n（已停止生成）"
                : "（已停止生成）";
        }
        catch (Exception ex)
        {
            reply.Text = buffer.Length > 0
                ? $"{buffer}\n\n⚠️ 连接中断：{ex.Message}"
                : $"⚠️ 调用失败：{ex.Message}";
        }
        finally
        {
            _progress.Progress -= OnProgress;
            reply.IsTyping = false;
            IsBusy = false;

            // 一轮问答结束（含停止 / 出错）：整会话写回 <guid>.json
            Persist();
        }
    }

    private bool CanStop() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    /// <summary>
    /// 写操作确认：工具线程发来请求 → 调度到 UI 线程插入确认卡。
    /// 卡片 Text 恒为空，SyncSession 会跳过，确认卡不入会话 json。
    /// </summary>
    private void OnConfirmationRequested(ConfirmationRequest request)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // 没有 UI 可展示（如无头环境）：直接拒绝，绝不静默放行
            request.Complete(approved: false);
            return;
        }

        dispatcher.Invoke(() =>
        {
            Messages.Add(new ChatMessage(string.Empty, isUser: false)
            {
                IsConfirmation = true,
                ConfirmationRequest = request,
                ConfirmationTitle = request.Title,
                ConfirmationPath = request.Detail,
                ConfirmationPreview = request.Preview,
                ConfirmationPending = true,
            });
        });
    }

    /// <summary>确认卡「同意」：放行挂起的写操作。</summary>
    [RelayCommand]
    private void ApproveConfirmation(ChatMessage? message)
    {
        if (message is { ConfirmationPending: true, ConfirmationRequest: { } request })
        {
            message.ConfirmationPending = false;
            message.ConfirmationOutcome = "✅ 已同意，工具继续执行";
            request.Complete(approved: true);
        }
    }

    /// <summary>确认卡「拒绝」：拦截挂起的写操作（工具将收到拒绝并如实告知模型）。</summary>
    [RelayCommand]
    private void DenyConfirmation(ChatMessage? message)
    {
        if (message is { ConfirmationPending: true, ConfirmationRequest: { } request })
        {
            message.ConfirmationPending = false;
            message.ConfirmationOutcome = "❌ 已拒绝，未写入任何内容";
            request.Complete(approved: false);
        }
    }
}
