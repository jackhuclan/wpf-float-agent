using System.IO;
using System.Text;
using System.Text.Json;
using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>
/// 基于 json 文件的会话存储：sessions/&lt;会话guid&gt;.json，整会话覆盖写（先写 tmp 再原子替换）。
/// 目录取运行目录（与 config.json 同一约定），目录不存在时自动创建。
/// </summary>
public sealed class JsonSessionStore : ISessionStore
{
    public const string FolderName = "sessions";

    private static readonly JsonSerializerOptions WriteOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // 中文不转义，文件直接用编辑器看
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions ReadOptions = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();

    public string SessionsDirectory { get; }

    public JsonSessionStore()
        : this(Path.Combine(Directory.GetCurrentDirectory(), FolderName))
    {
    }

    public JsonSessionStore(string sessionsDirectory)
    {
        SessionsDirectory = sessionsDirectory;
        Directory.CreateDirectory(SessionsDirectory);
    }

    public ChatSession StartNewSession() => new();

    public void Save(ChatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            Directory.CreateDirectory(SessionsDirectory);

            session.UpdatedAt = DateTimeOffset.Now;

            var path = Path.Combine(SessionsDirectory, session.FileName);
            var tempPath = path + ".tmp";

            File.WriteAllText(tempPath, JsonSerializer.Serialize(session, WriteOptions), Encoding.UTF8);

            // 原子替换：即使写到一半进程被结束，也不会留下半截 json
            File.Move(tempPath, path, overwrite: true);
        }
    }

    public IReadOnlyList<SessionSummary> List()
    {
        var result = new List<SessionSummary>();

        lock (_gate)
        {
            if (!Directory.Exists(SessionsDirectory))
            {
                return result;
            }

            foreach (var path in Directory.EnumerateFiles(SessionsDirectory, "*.json"))
            {
                try
                {
                    var session = JsonSerializer.Deserialize<ChatSession>(File.ReadAllText(path), ReadOptions);
                    if (session is null)
                    {
                        continue;
                    }

                    result.Add(new SessionSummary(
                        session.SessionId, session.Title,
                        session.CreatedAt, session.UpdatedAt, session.Messages.Count));
                }
                catch (Exception)
                {
                    // 损坏文件降级跳过：会话列表照常展示其余会话（NFR 可靠性）
                }
            }
        }

        return result.OrderByDescending(s => s.UpdatedAt).ToList();
    }

    public ChatSession? Load(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || Path.GetFileName(sessionId) != sessionId)
        {
            return null;    // 防路径穿越：sessionId 必须是纯 guid 文件名
        }

        lock (_gate)
        {
            var path = Path.Combine(SessionsDirectory, $"{sessionId}.json");
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<ChatSession>(File.ReadAllText(path), ReadOptions);
            }
            catch (Exception)
            {
                return null;    // 损坏文件按不存在处理
            }
        }
    }

    public void Delete(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || Path.GetFileName(sessionId) != sessionId)
        {
            return;
        }

        lock (_gate)
        {
            var path = Path.Combine(SessionsDirectory, $"{sessionId}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    public void Rename(string sessionId, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var session = Load(sessionId);
        if (session is null)
        {
            return;
        }

        session.Title = title.Trim();
        session.TitleLocked = true;
        Save(session);
    }
}
