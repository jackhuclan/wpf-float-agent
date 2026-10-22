using System.Threading;
using System.Threading.Tasks;
using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>连接测试结果。</summary>
/// <param name="Success">是否连通</param>
/// <param name="Message">给用户看的结果描述</param>
/// <param name="LatencyMs">耗时（毫秒）</param>
public sealed record ModelTestResult(bool Success, string Message, int LatencyMs);

/// <summary>
/// 模型连接测试：用给定配置发一次最小请求，验证 key/模型/端点是否可用。
/// </summary>
public interface IModelTester
{
    Task<ModelTestResult> TestAsync(ModelConfig config, CancellationToken cancellationToken = default);
}
