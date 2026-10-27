using System.IO;
using System.Net.Http;
using System.Windows;
using FloatAgent.Abstractions;
using Floaticon.Services;
using Floaticon.ViewModels;
using Floaticon.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecipeDemo.ViewModels;
using RecipeDemo.Views;

namespace RecipeDemo;

/// <summary>
/// RecipeDemo 启动入口：装配与 Floaticon 相同的最小服务集（配置 / Agent / 聊天），
/// 打开主窗（配方树 + 五视图）后同时显示 FloatAgent 里的悬浮球。
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 与 Floaticon 一致：工作目录锚定到 exe，config.json / skills 都以运行目录为准
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = e.Args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        ConfigureServices(builder.Services);

        _host = builder.Build();
        _host.Start();

        // 类库内的视图（ChatWindow 打开配置窗）通过 ServiceLocator 解析瞬时服务
        ServiceLocator.Services = _host.Services;

        // 主窗：配方树 + Summary / Wafer Map / Alignment / Tests / Recipe-Run Parameters
        _host.Services.GetRequiredService<MainWindow>().Show();

        // 需求：主窗打开时同时显示 FloatAgent 的悬浮球（点击弹出聊天窗）
        _host.Services.GetRequiredService<FloatingBallWindow>().Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigService, JsonConfigService>();
        services.AddSingleton<IModelTester, OpenAIModelTester>();
        services.AddSingleton<ToolCallLog>();
        services.AddSingleton<IAIToolService, AIToolService>();
        services.AddSingleton<ToolProgressService>();
        services.AddSingleton<HttpClient>();
        services.AddSingleton(sp => SkillProviderFactory.Create(sp.GetService<ILoggerFactory>()));

        services.AddSingleton<IChatService, AgentChatService>();

        // 会话持久化：每个会话一个 sessions/<guid>.json
        services.AddSingleton<ISessionStore, JsonSessionStore>();

        // 聊天窗 Hide 复用，必须是单例；配置窗 Close 后不可复用，用瞬时
        services.AddSingleton<ChatViewModel>();
        services.AddSingleton<ChatWindow>();
        services.AddSingleton<FloatingBallWindow>();
        services.AddTransient<ConfigViewModel>();
        services.AddTransient<ConfigWindow>();

        // 演示用主窗：五个视图各自独立，作为单例注入（工具在构造时注册到 IAIToolService）
        services.AddSingleton<SummaryViewModel>();
        services.AddSingleton<WaferMapViewModel>();
        services.AddSingleton<AlignmentViewModel>();
        services.AddSingleton<TestsViewModel>();
        services.AddSingleton<RecipeRunParametersViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
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
                // 退出阶段不再向上冒泡
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
