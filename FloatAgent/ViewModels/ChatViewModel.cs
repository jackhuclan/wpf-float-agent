using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using Floaticon.Models;
using Floaticon.Services;

namespace Floaticon.ViewModels;

/// <summary>
/// 聊天窗 ViewModel：消息集合 + 流式发送。
/// 通过 IChatService 调用 DeepSeek，回复逐字追加到气泡中。
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly IChatService _chatService;
    private readonly ToolProgressService _progress;
    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isBusy;

    public ChatViewModel(IChatService chatService, ToolProgressService progress)
    {
        _chatService = chatService;
        _progress = progress;
        Messages.Add(new ChatMessage(
            "你好，我是你的 AI 助手，已接入 DeepSeek。已加载技能：grill-me（方案拷问）、skill-creator（技能创作）、get-weather（天气查询），需要时我会自动调用。",
            isUser: false));
    }

    private bool CanSend() => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var userText = InputText.Trim();
        InputText = string.Empty;

        Messages.Add(new ChatMessage(userText, isUser: true));

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
        }
    }

    private bool CanStop() => IsBusy;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();
}
