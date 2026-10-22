using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Floaticon.Models;
using Floaticon.Services;

namespace Floaticon.ViewModels;

/// <summary>
/// 模型配置窗 ViewModel：
/// - 三项必填，实时校验（清空即标红、输入即消红）
/// - 保存到 config.json
/// - 测试连接：用当前表单值（未保存也行）发一次真实小请求
/// </summary>
public partial class ConfigViewModel : ObservableObject
{
    private readonly IConfigService _configService;
    private readonly IModelTester _modelTester;

    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _endpoint = string.Empty;

    /// <summary>可选模型列表（下拉框数据源）。</summary>
    public ObservableCollection<string> ModelOptions { get; } = new()
    {
        "qwen3:1.7b",
        "qwen3:8b",
        "deepseek-flash",
        "deepseek-v4-pro"
    };

    [ObservableProperty] private bool _apiKeyError;
    [ObservableProperty] private bool _ModelError;
    [ObservableProperty] private bool _endpointError;

    [ObservableProperty] private bool _isTesting;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(TestConnectionCommand))]
    private string _resultText = string.Empty;

    [ObservableProperty] private bool _resultIsSuccess;

    /// <summary>保存成功（由窗口订阅后关闭对话框）。</summary>
    public event EventHandler? Saved;

    public string ConfigPath => _configService.ConfigPath;

    public ConfigViewModel(IConfigService configService, IModelTester modelTester)
    {
        _configService = configService;
        _modelTester = modelTester;

        var current = configService.Current;
        _apiKey = current.ApiKey;
        _model = current.Model;
        _endpoint = current.Endpoint;

        // 已有配置里的模型如果不在预置列表里（例如旧值 deepseek-chat），补进去以免下拉框显示为空
        if (!string.IsNullOrWhiteSpace(current.Model) && !ModelOptions.Contains(current.Model))
        {
            ModelOptions.Insert(0, current.Model);
        }

        // 打开窗口时先校验一遍：本来就缺字段的话直接标红
        ValidateRequired();
    }

    // 实时校验：清空立即标红，输入立即消红
    partial void OnApiKeyChanged(string value) => ApiKeyError = string.IsNullOrWhiteSpace(value);
    partial void OnModelChanged(string value) => ModelError = string.IsNullOrWhiteSpace(value);
    partial void OnEndpointChanged(string value) => EndpointError = string.IsNullOrWhiteSpace(value);

    private void ValidateRequired()
    {
        ApiKeyError = string.IsNullOrWhiteSpace(ApiKey);
        ModelError = string.IsNullOrWhiteSpace(Model);
        EndpointError = string.IsNullOrWhiteSpace(Endpoint);
    }

    /// <summary>完整校验（必填 + 端点格式）。失败时把错误写进 ResultText 并返回 false。</summary>
    private bool TryBuildConfig(out ModelConfig config)
    {
        ValidateRequired();

        if (ApiKeyError || ModelError || EndpointError)
        {
            ResultIsSuccess = false;
            ResultText = "请填写所有必填项（标红处）";
            config = new ModelConfig();
            return false;
        }

        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            EndpointError = true;
            ResultIsSuccess = false;
            ResultText = "接口地址必须是 http/https 开头的完整地址";
            config = new ModelConfig();
            return false;
        }

        config = new ModelConfig
        {
            ApiKey = ApiKey.Trim(),
            Model = Model.Trim(),
            Endpoint = Endpoint.Trim()
        };
        return true;
    }

    private bool CanSave() => !IsTesting;
    private bool CanTest() => !IsTesting;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (!TryBuildConfig(out var config))
        {
            return;
        }

        try
        {
            _configService.Save(config);

            ResultIsSuccess = true;
            ResultText = "已保存，配置立即生效。";

            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ResultIsSuccess = false;
            ResultText = $"保存失败：{ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestConnectionAsync()
    {
        if (!TryBuildConfig(out var config))
        {
            return;
        }

        IsTesting = true;
        ResultIsSuccess = false;
        ResultText = "正在测试连接…";
        try
        {
            var result = await _modelTester.TestAsync(config);
            ResultIsSuccess = result.Success;
            ResultText = result.Message;
        }
        finally
        {
            IsTesting = false;
        }
    }
}
