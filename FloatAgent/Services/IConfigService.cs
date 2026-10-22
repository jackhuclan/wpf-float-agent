using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>
/// 模型配置读写。配置文件固定为当前目录下的 config.json。
/// </summary>
public interface IConfigService
{
    /// <summary>配置文件完整路径。</summary>
    string ConfigPath { get; }

    /// <summary>当前生效的配置（内存缓存）。</summary>
    ModelConfig Current { get; }

    /// <summary>保存配置并立即生效。</summary>
    void Save(ModelConfig config);
}
