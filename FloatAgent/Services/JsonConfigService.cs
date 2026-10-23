using System.IO;
using System.Text.Json;
using Floaticon.Models;

namespace Floaticon.Services;

/// <summary>
/// 基于 config.json 的配置实现。文件缺失或损坏时回落到默认配置。
/// </summary>
public sealed class JsonConfigService : IConfigService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ConfigPath { get; }

    public ModelConfig Current { get; private set; }

    public JsonConfigService()
    {
        ConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
        Current = Load();
    }

    public void Save(ModelConfig config)
    {
        var json = JsonSerializer.Serialize(config, WriteOptions);
        File.WriteAllText(ConfigPath, json);
        Current = config;
    }

    private ModelConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return ModelConfig.CreateDefault();
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<ModelConfig>(json, ReadOptions);
            return config is { IsValid: true } ? config : ModelConfig.CreateDefault();
        }
        catch (Exception)
        {
            // 配置损坏不阻塞启动，回落到默认值，用户在配置窗里重填即可
            return ModelConfig.CreateDefault();
        }
    }
}
