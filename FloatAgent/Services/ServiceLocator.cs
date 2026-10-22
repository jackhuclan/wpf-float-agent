namespace Floaticon.Services;

/// <summary>
/// 类库内的服务定位器：由启动工程（Floaticon）在 Host 构建完成后赋值，
/// 供类库内部的视图（如 ChatWindow）解析瞬时服务，避免类库反向依赖启动工程。
/// </summary>
public static class ServiceLocator
{
    public static IServiceProvider Services { get; set; } = default!;
}
