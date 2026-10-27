namespace Floaticon.Services;

/// <summary>
/// 一次写操作确认请求：由工具线程创建，聊天窗据此渲染确认卡；
/// 用户点「同意 / 拒绝」（或超时、取消）后经 <see cref="Complete"/> 放行或拦截。
/// </summary>
public sealed class ConfirmationRequest
{
    internal ConfirmationRequest(string toolName, string title, string detail, string? preview)
    {
        ToolName = toolName;
        Title = title;
        Detail = detail;
        Preview = preview;
    }

    /// <summary>发起确认的工具名（如 write_file）。</summary>
    public string ToolName { get; }

    /// <summary>确认卡标题（如「覆盖文件」「新建文件」）。</summary>
    public string Title { get; }

    /// <summary>关键详情（通常是目标文件完整路径）。</summary>
    public string Detail { get; }

    /// <summary>内容预览（写操作为将写入内容的前若干行），可为 null。</summary>
    public string? Preview { get; }

    internal TaskCompletionSource<bool> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>由 UI 侧调用：approved=true 放行，false 拦截。重复调用只生效一次。</summary>
    public void Complete(bool approved) => Completion.TrySetResult(approved);
}

/// <summary>
/// 写操作人工确认服务：工具在执行破坏性动作前请求人工确认，
/// 聊天窗订阅 <see cref="Requested"/> 弹确认卡；确认前工具线程被挂起。
/// </summary>
public interface IConfirmationService
{
    /// <summary>确认请求事件（在工具调用线程上触发，订阅方需自行调度到 UI 线程）。</summary>
    event Action<ConfirmationRequest>? Requested;

    /// <summary>
    /// 请求人工确认并挂起等待。返回 true 表示用户同意；
    /// 用户拒绝、确认超时（120 秒）、取消令牌触发（点「停止」）或无订阅者时返回 false。
    /// </summary>
    Task<bool> RequestConfirmationAsync(
        string toolName, string title, string detail, string? preview, CancellationToken cancellationToken);
}
