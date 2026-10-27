using System.Text;

namespace Floaticon.Services;

/// <summary>单条工具调用记录。</summary>
public sealed record ToolCallRecord(
    DateTime Timestamp,
    string ToolName,
    string Arguments,
    string Result,
    bool IsError,
    double DurationMs);

/// <summary>
/// 会话内工具执行日志：记录每次 AI 工具的真实调用（名称/参数/结果/耗时/是否异常）。
/// 通过 query_tool_history 工具暴露给模型，让模型能用记录核实"某一步是否真的执行过"，
/// 而不是凭记忆断言调用历史（实际运行中发现模型跨轮回忆调用记录不可靠）。
/// </summary>
public sealed class ToolCallLog
{
    private const int MaxRecords = 200;

    private readonly object _gate = new();
    private readonly List<ToolCallRecord> _records = [];

    public void Add(ToolCallRecord record)
    {
        lock (_gate)
        {
            _records.Add(record);
            if (_records.Count > MaxRecords)
            {
                _records.RemoveAt(0);
            }
        }
    }

    /// <summary>新会话开始时清空（工具日志以会话为边界）。</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _records.Clear();
        }
    }

    public IReadOnlyList<ToolCallRecord> Snapshot()
    {
        lock (_gate)
        {
            return [.. _records];
        }
    }

    /// <summary>格式化为给模型看的文本（最近 <paramref name="last"/> 条）。</summary>
    public string FormatForModel(int last = 10)
    {
        var snapshot = Snapshot();
        if (snapshot.Count == 0)
        {
            return "本次会话还没有任何工具调用记录。";
        }

        last = Math.Clamp(last, 1, MaxRecords);
        var sb = new StringBuilder();
        sb.AppendLine($"共 {snapshot.Count} 条记录，以下是最近 {Math.Min(last, snapshot.Count)} 条（按时间从旧到新）：");
        foreach (var r in snapshot.TakeLast(last))
        {
            sb.Append($"- [{r.Timestamp:HH:mm:ss}] {r.ToolName}({r.Arguments}) -> ");
            if (r.IsError)
            {
                sb.Append("[异常] ");
            }

            sb.AppendLine($"{r.Result}（耗时 {r.DurationMs:0}ms）");
        }

        return sb.ToString().TrimEnd();
    }
}
