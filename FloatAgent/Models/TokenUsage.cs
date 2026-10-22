using System;

namespace Floaticon.Models;

/// <summary>
/// 一次回复的 token 用量统计（来自模型返回的 usage 字段）。
/// </summary>
public sealed record TokenUsage(
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    string? Model = null)
{
    public override string ToString() => $"共消耗 {TotalTokens:N0} tokens（输入 {InputTokens:N0} / 输出 {OutputTokens:N0}）";
}
