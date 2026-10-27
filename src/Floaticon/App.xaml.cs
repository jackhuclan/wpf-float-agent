using System.IO;
using System.Net.Http;
using System.Windows;
using FloatAgent.Abstractions;
using Floaticon.Services;
using Floaticon.Tools;
using Floaticon.ViewModels;
using Floaticon.Views;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Floaticon;

/// <summary>
/// 启动壳：构建通用主机 → 注册服务 → Build/Start → 赋 ServiceLocator → 显示悬浮球。
/// 只负责装配，不含任何业务逻辑（业务全部在 FloatAgent 类库）。
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    /// <summary>容器出口，供启动工程内部（如调试钩子）解析服务。</summary>
    public static IServiceProvider Services =>
        Current is App { _host: { } host }
            ? host.Services
            : throw new InvalidOperationException("应用主机尚未初始化。");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 把工作目录锚定到 exe 目录：JsonConfigService / SkillProviderFactory 会回退到 CWD，
        // 这样无论双击、快捷方式还是命令行启动，config.json 都落在"运行目录"（FR-06）。
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            // 配置相对路径（config.json、skills）以 exe 所在目录为基准
            ContentRootPath = AppContext.BaseDirectory,
        });

        ConfigureServices(builder.Services);

        _host = builder.Build();
        _host.Start();

        // 类库内的视图（ChatWindow 打开配置窗）通过 ServiceLocator 解析瞬时服务，
        // 避免 FloatAgent 反向依赖启动工程。
        ServiceLocator.Services = _host.Services;

        RegisterTools(_host.Services);

        _host.Services.GetRequiredService<FloatingBallWindow>().Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 配置
        services.AddSingleton<IConfigService, JsonConfigService>();
        services.AddSingleton<IModelTester, OpenAIModelTester>();

        // 工具与技能
        services.AddSingleton<ToolCallLog>();
        services.AddSingleton<IAIToolService, AIToolService>();
        services.AddSingleton<ToolProgressService>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton<WeatherTool>();
        services.AddSingleton<LongRunningTool>();

        // 内置文件工具：白名单校验 + 写前人工确认
        services.AddSingleton<IConfirmationService, ConfirmationService>();
        services.AddSingleton(sp => new WorkspaceScope(sp.GetRequiredService<IConfigService>(), AppContext.BaseDirectory));
        services.AddSingleton<FileTools>();

        services.AddSingleton(sp => SkillProviderFactory.Create(sp.GetService<ILoggerFactory>()));

        // 聊天（AgentChatService 每次请求按当前配置重建 Agent，配置保存即生效）
        services.AddSingleton<IChatService, AgentChatService>();

        // 会话持久化：每个会话一个 sessions/<guid>.json
        services.AddSingleton<ISessionStore, JsonSessionStore>();

        // MVVM：ChatViewModel 为单例以保留历史消息；ConfigViewModel 每次打开配置窗新建
        services.AddSingleton<ChatViewModel>();
        services.AddTransient<ConfigViewModel>();

        // 窗口：聊天窗需与悬浮球长期共存（隐藏而非销毁），配置窗关闭后不可复用故为 Transient
        services.AddSingleton<ChatWindow>();
        services.AddSingleton<FloatingBallWindow>();
        services.AddTransient<ConfigWindow>();
    }

    /// <summary>把内置工具注册进工具表，Agent 会自动按需调用。</summary>
    private static void RegisterTools(IServiceProvider services)
    {
        var registry = services.GetRequiredService<IAIToolService>();
        var weather = services.GetRequiredService<WeatherTool>();
        var longRunning = services.GetRequiredService<LongRunningTool>();
        var progressService = services.GetRequiredService<ToolProgressService>();
        var fileTools = services.GetRequiredService<FileTools>();

        // 显式委托类型保住方法上的 [Description]，模型才能拿到参数说明
        Func<string, string?, CancellationToken, Task<string>> getWeather = weather.GetWeatherAsync;
        registry.AddTool(AIFunctionFactory.Create(getWeather, "get_weather"));

        Func<string, CancellationToken, Task<string>> runLongTask = longRunning.RunAsync;
        registry.AddTool(AIFunctionFactory.Create(runLongTask, "run_long_task"));

        // 内置文件工具：读取（行号分页）/ 写入（写前人工确认）
        Func<string, int, int, CancellationToken, Task<string>> readFile = fileTools.ReadFileAsync;
        registry.AddTool(AIFunctionFactory.Create(readFile, "read_file"));

        Func<string, string?, CancellationToken, Task<string>> writeFile = fileTools.WriteFileAsync;
        registry.AddTool(AIFunctionFactory.Create(writeFile, "write_file"));

        // 验证 PeriodicProgressService：模拟一个内部无法上报进度的第三方长耗时任务，
        // 由包装器在等待侧周期上报（开始 1 次 + 每 5 秒 1 次 + 完成 1 次）
        var periodic = PeriodicProgressService.Create(
            progressService, "thirdparty_long_run_task", SimulatedThirdPartyTaskAsync);
        Func<CancellationToken, Task<string>> runThirdparty = periodic.RunAsync;
        registry.AddTool(AIFunctionFactory.Create(
            runThirdparty, "thirdparty_long_run_task", "执行一个模拟的第三方长时间运行任务（约 15 秒，内部无法上报进度，由包装器周期上报执行进度）。当用户要求验证第三方长任务、或询问 PeriodicProgressService 的效果时调用。"));

        // 验证 ContainerCodeTool：在一次性 Linux 容器中执行一段演示 C# 代码
        //（构造函数传内联代码文本；也支持传脚本路径，按扩展名自动识别语言）。
        var containerDemo = new ContainerCodeTool(
            progressService,
            """
            using System;

            Console.WriteLine($"Hello from Linux container! .NET {Environment.Version}");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Machine: {Environment.MachineName}");
            """,
            language: "csharp");
        Func<CancellationToken, Task<string>> runContainerCode = containerDemo.RunAsync;
        registry.AddTool(AIFunctionFactory.Create(
            runContainerCode, "run_container_code", "在一次性 Linux 容器中执行一段演示 C# 代码并返回标准输出/错误/退出码（宿主机无需 .NET 运行时）。当用户要求验证容器内代码执行、或询问 ContainerCodeTool / DockerShellExecutor 的效果时调用。首次运行需要拉取镜像，可能耗时数分钟。"));
    }

    /// <summary>模拟的第三方长耗时任务：约 15 秒，内部没有任何进度上报能力。</summary>
    private static async Task SimulatedThirdPartyTaskAsync(CancellationToken cancellationToken)
    {
        // 模拟第三方分步工作（此处不可改动——如同真实第三方库）
        await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is { } host)
        {
            try
            {
                host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // 退出阶段不再向上冒泡，避免关闭流程被阻断
            }
            finally
            {
                host.Dispose();
                _host = null;
            }
        }

        base.OnExit(e);
    }
}
