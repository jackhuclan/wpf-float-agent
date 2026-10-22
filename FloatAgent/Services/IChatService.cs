using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>
/// 流式返回的一个分片：通常是文本增量；最后一次可能只携带 token 用量。
/// </summary>
public sealed record StreamChunk(string? Text, TokenUsage? Usage);

/// <summary>
/// 聊天服务抽象：以流式（逐 token）方式返回模型回复。
/// 替换实现即可切换模型供应商，ViewModel 无需改动。
/// </summary>
public interface IChatService
{
    IAsyncEnumerable<StreamChunk> StreamReplyAsync(
        IReadOnlyList<ChatTurn> history,
        CancellationToken cancellationToken = default);
}
