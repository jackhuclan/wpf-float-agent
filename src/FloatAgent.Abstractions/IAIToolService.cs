using Microsoft.Extensions.AI;

namespace FloatAgent.Abstractions;

/// <summary>
/// AI 工具注册表：各处把 AIFunction 注册进来，AgentChatService 统一取出交给 Agent。
/// </summary>
public interface IAIToolService
{
    void AddTool(Delegate method, string name, string description);
    void AddTool(AIFunction aIFunction);
    IList<AITool> AsTools();
}
