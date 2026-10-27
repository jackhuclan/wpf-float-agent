using Floaticon.Models;

namespace Floaticon.Services;

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
}
