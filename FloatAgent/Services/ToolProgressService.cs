using System;

namespace Floaticon.Services;

/// <summary>
/// 工具执行进度广播（单例）。
/// 工具在执行期间调用 <see cref="Report"/>，聊天界面订阅 <see cref="Progress"/>
/// 把进度展示在“正在输入”气泡里，解决长耗时工具执行期间界面零反馈的问题。
/// 事件在线程池线程上触发，订阅方需自行调度到 UI 线程。
/// </summary>
public sealed class ToolProgressService
{
    /// <summary>进度事件，参数为可直接展示的文本。</summary>
    public event Action<string>? Progress;

    /// <summary>广播一条进度文本；无订阅者时为空操作。</summary>
    public void Report(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            Progress?.Invoke(message);
        }
    }
}
