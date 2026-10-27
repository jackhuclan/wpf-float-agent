using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Floaticon.Services;

/// <summary>
/// AIFunction 装饰器：在真实调用前后把 名称/参数/结果/耗时 写入 <see cref="ToolCallLog"/>，
/// 元数据（名称/描述/schema/底层方法）全部透传内部函数，行为与被包装函数完全一致。
/// </summary>
internal sealed class LoggedAIFunction : AIFunction
{
    private readonly AIFunction _inner;
    private readonly ToolCallLog _log;

    public LoggedAIFunction(AIFunction inner, ToolCallLog log)
    {
        _inner = inner;
        _log = log;
    }

    public override string Name => _inner.Name;

    public override string Description => _inner.Description;

    public override JsonElement JsonSchema => _inner.JsonSchema;

    public override JsonElement? ReturnJsonSchema => _inner.ReturnJsonSchema;

    public override MethodInfo? UnderlyingMethod => _inner.UnderlyingMethod;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var argsText = SafeArguments(arguments);
        try
        {
            var result = await _inner.InvokeAsync(arguments, cancellationToken).ConfigureAwait(false);
            _log.Add(new ToolCallRecord(
                DateTime.Now, Name, argsText, SafeResult(result), IsError: false, sw.Elapsed.TotalMilliseconds));
            return result;
        }
        catch (OperationCanceledException)
        {
            _log.Add(new ToolCallRecord(
                DateTime.Now, Name, argsText, "调用被取消", IsError: true, sw.Elapsed.TotalMilliseconds));
            throw;
        }
        catch (Exception ex)
        {
            _log.Add(new ToolCallRecord(
                DateTime.Now, Name, argsText, ex.Message, IsError: true, sw.Elapsed.TotalMilliseconds));
            throw;
        }
    }

    private static string SafeArguments(AIFunctionArguments arguments)
    {
        if (arguments.Count == 0)
        {
            return string.Empty;
        }

        try
        {
            return JsonSerializer.Serialize(new Dictionary<string, object?>(arguments));
        }
        catch
        {
            return "<参数无法序列化>";
        }
    }

    private static string SafeResult(object? result)
    {
        if (result is null)
        {
            return "(无返回)";
        }

        // AIFunctionFactory 会把返回值序列化成 JsonElement：字符串取原值，其余取原文
        if (result is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var s = je.GetString();
                return s is null ? "(无返回)" : (s.Length <= 300 ? s : s[..300] + "…");
            }

            var raw = je.GetRawText();
            return raw.Length <= 300 ? raw : raw[..300] + "…";
        }

        if (result is string s2)
        {
            return s2.Length <= 300 ? s2 : s2[..300] + "…";
        }

        try
        {
            var json = JsonSerializer.Serialize(result);
            return json.Length <= 300 ? json : json[..300] + "…";
        }
        catch
        {
            return result.ToString() ?? "(无返回)";
        }
    }
}
