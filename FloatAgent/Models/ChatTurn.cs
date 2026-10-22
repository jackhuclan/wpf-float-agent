namespace Floaticon.Models;

/// <summary>
/// 一轮对话消息（发送给模型的最小单元）。
/// </summary>
/// <param name="Role">"user" 或 "assistant"</param>
/// <param name="Content">消息正文</param>
public sealed record ChatTurn(string Role, string Content);
