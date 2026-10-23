using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Floaticon.Tools;

/// <summary>
/// 长耗时任务工具：模拟一个固定耗时 2 分钟的作业。
/// 方法通过 <see cref="Microsoft.Extensions.AI.AIFunctionFactory"/> 包装成 run_long_task 函数，
/// 由 IAIToolService 注册后交给 Agent 自动调用。
/// </summary>
/// <remarks>
/// 该工具用于验证 Agent 在工具长时间执行时的整链路行为：
/// 工具在本地线程池上异步等待，期间不占用模型连接，也不阻塞 UI；
/// 工具返回后 Agent 会用结果继续发起下一轮模型调用并输出最终答复。
/// 等待过程按 1 秒切片，用户点击“停止”时能立即响应取消。
/// </remarks>
public sealed class LongRunningTool
{
    /// <summary>任务固定耗时：2 分钟。</summary>
    private static readonly TimeSpan Duration = TimeSpan.FromMinutes(2);

    /// <summary>等待切片，用于保证取消能及时生效。</summary>
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    /// <summary>执行一个耗时约 2 分钟的长任务，结束后返回执行结果。</summary>
    [Description("执行一个耗时约 2 分钟（120 秒）的长耗时任务，任务完成后返回执行结果。" +
                 "当用户要求测试长耗时任务、或明确要求执行一个需要较长时间才能完成的作业时调用。" +
                 "调用期间不会有中间输出，模型需等待工具返回后再向用户汇报。")]
    public async Task<string> RunAsync(
        [Description("任务名称或要执行的事情，用于标识本次长任务，例如：端到端耗时验证")] string task,
        CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(task) ? "长耗时任务" : task.Trim();
        var startedAt = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // 逐秒等待：精确凑满 2 分钟，同时让取消在 1 秒内生效
            while (stopwatch.Elapsed < Duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var remaining = Duration - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    break;
                }

                await Task.Delay(remaining < Tick ? remaining : Tick, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // 取消语义交回上层：ChatViewModel 会展示“已停止生成”
            stopwatch.Stop();
            throw;
        }

        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new StringBuilder();
        builder.AppendLine("**长耗时任务执行完成**");
        builder.AppendLine();
        builder.AppendLine($"- 任务：{name}");
        builder.AppendLine("- 状态：成功");
        builder.AppendLine($"- 开始时间：{startedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- 结束时间：{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- 实际耗时：{stopwatch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} 秒");
        builder.AppendLine($"- 任务编号：{Guid.NewGuid():N}");
        builder.AppendLine();
        builder.Append("说明：本次为模拟负载（固定 2 分钟），结果可用于确认工具长耗时执行后 Agent 仍能正常续答。");

        return builder.ToString().TrimEnd();
    }
}
