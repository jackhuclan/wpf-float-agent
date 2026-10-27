namespace Floaticon.Services;

/// <summary>
/// IConfirmationService 默认实现：TaskCompletionSource 挂起等待用户决定。
/// 无订阅者、超时（120 秒）、取消令牌触发一律视为拒绝。
/// </summary>
public sealed class ConfirmationService : IConfirmationService
{
    /// <summary>确认等待超时：用户可能暂时离开，超时按拒绝处理避免工具线程永久挂起。</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public event Action<ConfirmationRequest>? Requested;

    public async Task<bool> RequestConfirmationAsync(
        string toolName, string title, string detail, string? preview, CancellationToken cancellationToken)
    {
        // 没有任何 UI 订阅确认事件（如无头环境）时直接拒绝，绝不静默放行
        if (Requested is null)
        {
            return false;
        }

        var request = new ConfirmationRequest(toolName, title, detail, preview);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(Timeout);

        // 超时或取消（含用户点"停止"）都按拒绝完成，避免永久挂起
        using (linkedCts.Token.Register(() => request.Complete(approved: false)))
        {
            Requested?.Invoke(request);
            return await request.Completion.Task.ConfigureAwait(false);
        }
    }
}
