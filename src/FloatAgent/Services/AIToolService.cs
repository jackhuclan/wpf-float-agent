using System.ComponentModel;
using FloatAgent.Abstractions;
using Microsoft.Extensions.AI;

namespace Floaticon.Services;

/// <summary>
/// 以函数名为键的工具容器，重复注册同名工具时后者覆盖前者。
/// 注册的每个 AIFunction 都会被 <see cref="LoggedAIFunction"/> 包装，
/// 真实调用（名称/参数/结果/耗时）自动写入 <see cref="ToolCallLog"/>，
/// 并内置 query_tool_history 工具供模型查证调用历史。
/// </summary>
public class AIToolService : IAIToolService
{
    private readonly Dictionary<string, AITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly ToolCallLog _log;

    public AIToolService(ToolCallLog log)
    {
        _log = log;
        RegisterQueryTool();
    }

    public void AddTool(AIFunction aIFunction)
    {
        var wrapped = aIFunction is LoggedAIFunction ? aIFunction : new LoggedAIFunction(aIFunction, _log);
        _tools[wrapped.Name] = wrapped;
    }

    public void AddTool(Delegate method, string name, string description)
    {
        AddTool(AIFunctionFactory.Create(method, name, description));
    }

    public IList<AITool> AsTools() => _tools.Values.ToList();

    /// <summary>内置工具：查证本会话内工具的真实调用记录。</summary>
    private void RegisterQueryTool()
    {
        AddTool((Func<int, string>)QueryToolHistoryTool,
            "query_tool_history",
            "查询本次会话中已真实执行的工具调用记录（时间、名称、参数、结果、耗时、是否异常）。" +
            "当你需要核实某一步工具是否真的执行过、结果是什么时，调用本工具查证；" +
            "禁止凭记忆断言调用历史。若你怀疑自己曾输出过未经调用支撑的结果，也用它核对。");
    }

    [Description("返回最近 N 条记录，默认 10")]
    private string QueryToolHistoryTool([Description("返回最近 N 条记录")] int last = 10) => _log.FormatForModel(last);
}
