using FloatAgent.Abstractions;
using Microsoft.Extensions.AI;

namespace Floaticon.Services;

/// <summary>
/// 以函数名为键的工具容器，重复注册同名工具时后者覆盖前者。
/// </summary>
public class AIToolService : IAIToolService
{
    private readonly Dictionary<string, AITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    public void AddTool(AIFunction aIFunction)
    {
        _tools[aIFunction.Name] = aIFunction;
    }

    public IList<AITool> AsTools() => _tools.Values.ToList();
}
