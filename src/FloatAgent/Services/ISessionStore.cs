using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>
/// 会话列表项摘要（轻量，不携带消息正文）。
/// </summary>
public sealed record SessionSummary(
    string SessionId,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int MessageCount);

/// <summary>
/// 会话持久化：一个会话一个 json 文件，文件名为会话 guid。
/// </summary>
public interface ISessionStore
{
    /// <summary>会话文件所在目录（运行目录下的 sessions/）。</summary>
    string SessionsDirectory { get; }

    /// <summary>开启一次新会话（生成新 guid，尚未落盘）。</summary>
    ChatSession StartNewSession();

    /// <summary>把会话整体写回 sessions/&lt;guid&gt;.json（覆盖写，原子替换）。</summary>
    void Save(ChatSession session);

    /// <summary>
    /// 列出全部会话摘要（按最后更新时间倒序）。
    /// 损坏文件跳过不抛异常（降级），不阻塞会话列表展示。
    /// </summary>
    IReadOnlyList<SessionSummary> List();

    /// <summary>按 guid 加载完整会话；文件不存在或损坏返回 null。</summary>
    ChatSession? Load(string sessionId);

    /// <summary>删除会话文件；文件不存在时不作任何改动。</summary>
    void Delete(string sessionId);

    /// <summary>重命名会话并落盘（TitleLocked 置真，自动标题不再覆盖）。</summary>
    void Rename(string sessionId, string title);
}
