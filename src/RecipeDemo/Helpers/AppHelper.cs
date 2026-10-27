using System.Windows;
using System.Windows.Threading;

namespace RecipeDemo.Helpers;

/// <summary>
/// 应用级小工具：AI 工具函数在后台线程被调用，而 ViewModel 的 ObservableProperty 与联动 UI
/// 必须在 UI 线程上变更，统一走这里投递。
/// </summary>
public static class AppHelper
{
    /// <summary>同步投递到 UI 线程执行；已在 UI 线程则直接执行。</summary>
    public static void DispatcherInvoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        // 同步等待：保证工具返回前界面状态已生效（技能编排是串行的"切换视图→再计算"）
        dispatcher.Invoke(action, DispatcherPriority.Normal);
    }

    /// <summary>同步投递到 UI 线程执行并取回结果。</summary>
    public static T DispatcherInvoke<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            return func();
        }

        return dispatcher.Invoke(func, DispatcherPriority.Normal);
    }
}
