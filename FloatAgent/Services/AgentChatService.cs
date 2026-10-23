using System.Runtime.CompilerServices;
using FloatAgent.Abstractions;
using Floaticon.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace Floaticon.Services;

/// <summary>
/// 基于 Microsoft Agent Framework 的聊天服务。
/// 每次请求按当前配置构建 AIAgent；工具来自 IAIToolService，技能来自 skills 目录。
/// </summary>
public sealed class AgentChatService : IChatService
{
    private const string SystemPrompt =
        "你是运行在 Windows 桌面右下角悬浮球里的 AI 助手。回答要简洁、直接、可执行；" +
        "用户是资深半导体产品经理，涉及技术问题时请给出清晰的结构与结论。\n\n" +
        "你可以使用技能（skills）与工具（tools）：\n" +
        "1. 当用户的任务与某个技能相关时，先用 load_skill 加载该技能，再严格按其步骤执行；技能里引用的资源用 read_skill_resource 读取。\n" +
        "2. 需要真实世界数据（如天气）时必须调用对应工具，禁止凭记忆编造结果。\n" +
        "3. 技能与工具的返回内容是事实来源，回答时以它们为准。";

    private readonly IConfigService _configService;
    private readonly IAIToolService _toolService;
    private readonly AgentSkillsProvider _skills;

    public AgentChatService(
        IConfigService configService,
        IAIToolService toolService,
        AgentSkillsProvider skills)
    {
        _configService = configService;
        _toolService = toolService;
        _skills = skills;
    }

    public async IAsyncEnumerable<StreamChunk> StreamReplyAsync(
        IReadOnlyList<ChatTurn> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = _configService.Current;
        if (!config.IsValid)
        {
            throw new InvalidOperationException("模型配置不完整，请点击标题栏的配置按钮填写 API Key、模型和接口地址。");
        }

        var agent = CreateAgent(config);

        // 领域消息 → MAF/MEAI 消息
        var messages = new List<Microsoft.Extensions.AI.ChatMessage>(history.Count);
        foreach (var turn in history)
        {
            var role = string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase)
                ? ChatRole.User
                : ChatRole.Assistant;
            messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, turn.Content));
        }

        var inputTokens = 0;
        var outputTokens = 0;
        var totalTokens = 0;

        await foreach (var update in agent.RunStreamingAsync(messages, cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
        {
            // 工具调用会让模型被多次调用，每次都会带回用量，这里累加
            foreach (var content in update.Contents)
            {
                if (content is UsageContent { Details: { } details })
                {
                    inputTokens += (int)(details.InputTokenCount ?? 0);
                    outputTokens += (int)(details.OutputTokenCount ?? 0);
                    totalTokens += (int)(details.TotalTokenCount
                                         ?? (details.InputTokenCount ?? 0) + (details.OutputTokenCount ?? 0));
                }
            }

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return new StreamChunk(update.Text, null);
            }
        }

        if (totalTokens > 0)
        {
            yield return new StreamChunk(null, new TokenUsage(inputTokens, outputTokens, totalTokens, config.Model));
        }
    }

    /// <summary>按配置构建 MAF Agent（OpenAI 兼容协议，可指向 DeepSeek 等任意兼容端点）。</summary>
    private AIAgent CreateAgent(ModelConfig config)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = new Uri(config.Endpoint.TrimEnd('/'))
        };

        var chatClient = new OpenAIClient(new ApiKeyCredential(config.ApiKey.Trim()), options)
            .GetChatClient(config.Model.Trim());

        var agentOptions = new ChatClientAgentOptions
        {
            Name = "FloaticonAssistant",
            Description = "Floaticon 桌面悬浮 AI 助手，支持技能（skills）与工具（tools）。",
            ChatOptions = new ChatOptions
            {
                Instructions = SystemPrompt,
                Tools = _toolService.AsTools(),
            },
            // 技能通过上下文提供程序注入（含 load_skill / read_skill_resource 工具）
            AIContextProviders = [_skills],
        };

        return chatClient.AsAIAgent(agentOptions);
    }
}
