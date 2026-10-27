using System.Text.Json.Serialization;

namespace Floaticon.Models;

/// <summary>
/// 模型配置：三项均必填。持久化到运行目录（exe 所在目录）的 config.json。
/// </summary>
public sealed class ModelConfig
{
    /// <summary>API Key，例如 sk-xxxx。</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>模型名称，例如 deepseek-chat。</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>OpenAI 兼容的接口地址，例如 https://api.deepseek.com/v1。</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// 文件工具（read_file / write_file）的附加工作区根目录（可选，默认仅 exe 目录）。
    /// 相对/绝对路径均可；保存时未配置则不写出该字段。
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? WorkspaceRoots { get; set; }

    [JsonIgnore]
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model) &&
        !string.IsNullOrWhiteSpace(Endpoint);

    /// <summary>
    /// 首次运行（无 config.json）时的默认值。
    /// 注意：API Key 属于敏感凭据，代码中不保留任何默认值，必须由用户在配置窗
    /// 或 config.json 中填写；模型与端点只给定非敏感的预置值方便开箱使用。
    /// </summary>
    public static ModelConfig CreateDefault() => new()
    {
        ApiKey = string.Empty,
        Model = "deepseek-chat",
        Endpoint = "https://api.deepseek.com/v1"
    };
}
