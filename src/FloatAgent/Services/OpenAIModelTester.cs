using System.Diagnostics;
using Floaticon.Models;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;

namespace Floaticon.Services;

/// <summary>
/// 用 OpenAI 兼容协议做真实连通性测试：
/// 以用户填写的 Key/模型/端点发一条 1 token 级别的小请求。
/// </summary>
public sealed class OpenAIModelTester : IModelTester
{
    public async Task<ModelTestResult> TestAsync(ModelConfig config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri(config.Endpoint.TrimEnd('/'))
            };

            var chatClient = new OpenAIClient(new ApiKeyCredential(config.ApiKey.Trim()), options)
                .GetChatClient(config.Model.Trim());

            var completionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 16,
                Temperature = 0
            };

            var completion = await chatClient.CompleteChatAsync(
                [new UserChatMessage("ping")],
                completionOptions,
                cancellationToken).ConfigureAwait(false);

            sw.Stop();
            var reply = completion.Value.Content.Count > 0
                ? completion.Value.Content[0].Text?.Trim()
                : null;

            var preview = string.IsNullOrEmpty(reply) ? "" : $"，回复「{Truncate(reply!, 24)}」";
            return new ModelTestResult(
                true,
                $"连接成功：{config.Model} @ {config.Endpoint}，耗时 {sw.ElapsedMilliseconds} ms{preview}",
                (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return new ModelTestResult(false, "测试已取消。", (int)sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ModelTestResult(false, $"连接失败：{Condense(ex)}", (int)sw.ElapsedMilliseconds);
        }
    }

    /// <summary>把异常压成一两句话，避免把堆栈整段甩给用户。</summary>
    private static string Condense(Exception ex)
    {
        var message = ex.Message;
        // HttpClientException 通常自带完整 JSON 错误体，截掉多余部分
        var newline = message.IndexOfAny(['\n', '\r']);
        if (newline > 0)
        {
            message = message[..newline];
        }
        return message.Length > 160 ? message[..160] + "…" : message;
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
