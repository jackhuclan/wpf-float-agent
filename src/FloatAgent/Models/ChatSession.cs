namespace Floaticon.Models;

using System.Text.Json.Serialization;

/// <summary>
/// 会话中一条已落盘的消息（只保留持久化需要的字段，界面态如"正在输入"不入盘）。
/// </summary>
public sealed class SessionMessage
{
    /// <summary>"user" 或 "assistant"。</summary>
    public string Role { get; init; } = "assistant";

    public string Text { get; init; } = string.Empty;

    /// <summary>消息时间（用户气泡与 AI 气泡的显示时间都取自这里）。</summary>
    public DateTime Timestamp { get; init; } = DateTime.Now;

    /// <summary>AI 消息的 token 用量（用户消息为 null）。</summary>
    public int? TotalTokens { get; init; }

    /// <summary>生成该回复的模型名（用户消息为 null）。</summary>
    public string? Model { get; init; }
}

/// <summary>
/// 一次会话：SessionId 即会话 guid，对应 sessions/&lt;guid&gt;.json 一个文件。
/// </summary>
public sealed class ChatSession
{
    /// <summary>会话唯一标识（文件名的来源）。</summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("D");

    /// <summary>会话标题：取第一条用户消息的前若干字符，空会话为「新会话」。</summary>
    public string Title { get; set; } = "新会话";

    /// <summary>标题是否已被用户手动重命名（真则 SyncSession 不再用首条消息覆盖标题）。</summary>
    public bool TitleLocked { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>消息列表（反序列化时填充到既有集合，因此保留只读属性）。</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<SessionMessage> Messages { get; } = [];

    /// <summary>会话文件名：&lt;guid&gt;.json。</summary>
    public string FileName => $"{SessionId}.json";
}
