using System.Diagnostics;
using System.Globalization;
using System.Text;
using Floaticon.Services;

namespace Floaticon.Tools;

/// <summary>
/// 第三方长耗时任务的周期性进度包装器。
/// 第三方方法内部无法注入 <see cref="ToolProgressService"/> 上报进度时，
/// 用本类在"等待侧"代替它上报：开始 1 次、执行中每 <see cref="Interval"/> 周期 1 次、结束 1 次，
/// 第三方代码零改动。
/// </summary>
/// <remarks>
/// 典型用法（一个第三方任务对应一个实例，注册时绑定）：
/// <code>
/// var periodic = new PeriodicProgressService(
///     progressService, "thirdparty_long_run_task", ThirdPartyLib.LongRunAsync);
///
/// // RunAsync 只剩 CancellationToken 一个参数，显式委托可保留签名
/// Func&lt;CancellationToken, Task&lt;string&gt;&gt; run = periodic.RunAsync;
/// registry.AddTool(AIFunctionFactory.Create(
///     run, "thirdparty_long_run_task", "第三方的长时间运行任务"));
/// </code>
/// 注意：不要用 lambda（如 <c>() => Task.Run(work)</c>）注册——lambda 上的
/// CancellationToken 参数不会接线，工具将无法被"停止"取消。
/// </remarks>
public sealed class PeriodicProgressService
{
    private readonly ToolProgressService _progress;
    private readonly Func<CancellationToken, Task> _work;
    private string? _lastResult;

    /// <summary>任务名称，出现在每条进度文本中。</summary>
    public string Name { get; }

    /// <summary>周期上报间隔，默认 5 秒。</summary>
    public TimeSpan Interval { get; }

    /// <summary>包装一个接收取消令牌的第三方异步任务。</summary>
    public PeriodicProgressService(
        ToolProgressService progress,
        string name,
        Func<CancellationToken, Task> work,
        TimeSpan? interval = null)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Name = string.IsNullOrWhiteSpace(name) ? "第三方方法" : name.Trim();
        _work = work ?? throw new ArgumentNullException(nameof(work));
        Interval = interval ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>包装一个不接收取消令牌的第三方异步任务。</summary>
    public PeriodicProgressService(
        ToolProgressService progress,
        string name,
        Func<Task> work,
        TimeSpan? interval = null)
        : this(progress, name, _ => work(), interval)
    {
    }

    /// <summary>包装一个同步的第三方任务（在线程池上执行）。</summary>
    public PeriodicProgressService(
        ToolProgressService progress,
        string name,
        Action work,
        TimeSpan? interval = null)
        : this(progress, name, _ => Task.Run(work), interval)
    {
    }

    /// <summary>包装一个返回字符串结果的第三方异步任务，结果会附在完成汇报里。</summary>
    public PeriodicProgressService(
        ToolProgressService progress,
        string name,
        Func<CancellationToken, Task<string>> work,
        TimeSpan? interval = null)
    {
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        Name = string.IsNullOrWhiteSpace(name) ? "第三方方法" : name.Trim();
        _work = async ct => _lastResult = await work(ct).ConfigureAwait(false);
        Interval = interval ?? TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// 执行包装的第三方任务并周期性上报进度：
    /// 开始时上报「{Name} 开始执行...」，等待期间每 <see cref="Interval"/> 上报
    /// 「{Name} 正在执行... 已运行 N 秒」，结束后上报「{Name} 执行完成」。
    /// 返回结构化的执行结果文本（交给模型作为事实来源）。
    /// </summary>
    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();

        _progress.Report($"⚡ {Name} 开始执行...");

        // 第三方在独立任务上跑，主循环只负责"等待 + 周期上报"，互不阻塞
        var workTask = Task.Run(() => _work(cancellationToken), cancellationToken);

        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // 第三方已完成（含抛异常）就退出循环；否则等到下个周期再报一次
                if (workTask.IsCompleted)
                {
                    break;
                }

                var finished = await Task.WhenAny(workTask, Task.Delay(Interval, cancellationToken))
                    .ConfigureAwait(false);

                if (finished != workTask)
                {
                    // 竞态兜底：任务恰在本周期边界完成时，不再多报一次"正在执行"
                    if (workTask.IsCompleted)
                    {
                        break;
                    }

                    _progress.Report($"⚡ {Name} 正在执行... 已运行 {stopwatch.Elapsed.TotalSeconds:0} 秒");
                }
            }

            // 等待收尾：观察第三方任务的异常/取消，保证不吞掉失败
            await workTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 取消语义交回上层（ChatViewModel 显示"已停止生成"）
            stopwatch.Stop();
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _progress.Report($"❌ {Name} 执行失败");
            return BuildResult(startedAt, stopwatch.Elapsed, "失败", $"执行出错：{ex.Message}");
        }

        stopwatch.Stop();
        cancellationToken.ThrowIfCancellationRequested();

        _progress.Report($"✅ {Name} 执行完成，耗时 {stopwatch.Elapsed.TotalSeconds:0.0} 秒");
        return BuildResult(startedAt, stopwatch.Elapsed, "成功",
            _lastResult is null ? null : $"第三方返回：{_lastResult}");
    }

    private string BuildResult(DateTime startedAt, TimeSpan elapsed, string status, string? extra)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"**{Name} 执行{status}**");
        builder.AppendLine();
        builder.AppendLine($"- 状态：{status}");
        builder.AppendLine($"- 开始时间：{startedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- 结束时间：{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"- 实际耗时：{elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)} 秒");

        if (!string.IsNullOrWhiteSpace(extra))
        {
            builder.AppendLine($"- {extra}");
        }

        return builder.ToString().TrimEnd();
    }
}
