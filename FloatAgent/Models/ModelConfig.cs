using System;
using System.Text.Json.Serialization;

namespace Floaticon.Models;

/// <summary>
/// 模型配置：三项均必填。持久化到当前目录的 config.json。
/// </summary>
public sealed class ModelConfig
{
    /// <summary>API Key，例如 sk-xxxx。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名称，例如 deepseek-chat。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>OpenAI 兼容的接口地址，例如 https://api.deepseek.com/v1。</summary>
    public string Endpoint { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model) &&
        !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>首次运行（无 config.json）时的默认值。</summary>
    public static ModelConfig CreateDefault() => new()
    {
        ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                 ?? "sk-98956f4269244702aa321d4b3f4f57b7",
        Model = "deepseek-chat",
        Endpoint = "https://api.deepseek.com/v1"
    };
}
