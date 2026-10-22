using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Floaticon.Views.Behaviors;

/// <summary>
/// 让窗口"始终在最上面"的保活机制。
/// XAML 的 Topmost=True 只是一次性声明：当出现其它同级别置顶窗口（如部分桌面工具）、
/// 或系统窗口仲裁后，本窗口可能被压下去。这里用低频定时器 + Deactivated 事件
/// 周期性重新声明 HWND_TOPMOST，保证悬浮球 / 聊天窗 / 配置窗任何时刻都在最前。
/// </summary>
public static class TopmostKeeper
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1500);

    public static readonly DependencyProperty KeepTopmostProperty =
        DependencyProperty.RegisterAttached(
            "KeepTopmost", typeof(bool), typeof(TopmostKeeper),
            new PropertyMetadata(false, OnKeepTopmostChanged));

    private static readonly DependencyProperty TimerProperty =
        DependencyProperty.RegisterAttached("Timer", typeof(DispatcherTimer), typeof(TopmostKeeper), new PropertyMetadata(null));

    [AttachedPropertyBrowsableForType(typeof(Window))]
    public static bool GetKeepTopmost(DependencyObject obj) => (bool)obj.GetValue(KeepTopmostProperty);
    public static void SetKeepTopmost(DependencyObject obj, bool value) => obj.SetValue(KeepTopmostProperty, value);

    private static void OnKeepTopmostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window win)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            win.Deactivated += OnDeactivated;
            win.Loaded += OnLoaded;
            win.Closed += OnClosed;
            if (win.IsLoaded)
            {
                Start(win);
            }
        }
        else
        {
            win.Deactivated -= OnDeactivated;
            win.Loaded -= OnLoaded;
            win.Closed -= OnClosed;
            Stop(win);
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Start((Window)sender);
    private static void OnDeactivated(object? sender, EventArgs e) => EnsureTopmost((Window)sender!);
    private static void OnClosed(object? sender, EventArgs e) => Stop((Window)sender!);

    private static void Start(Window win)
    {
        if (win.GetValue(TimerProperty) is DispatcherTimer)
        {
            return;
        }

        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = Interval };
        timer.Tick += (_, _) => EnsureTopmost(win);
        timer.Start();
        win.SetValue(TimerProperty, timer);
        EnsureTopmost(win);
    }

    private static void Stop(Window win)
    {
        if (win.GetValue(TimerProperty) is DispatcherTimer timer)
        {
            timer.Stop();
            win.SetValue(TimerProperty, null);
        }
    }

    private static void EnsureTopmost(Window win)
    {
        // 隐藏的窗口（如收起的聊天窗）不必处理，Show 时会重新走这里
        if (!win.IsVisible || win.WindowState == WindowState.Minimized)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(win).EnsureHandle();
        if (win.Topmost == false)
        {
            win.Topmost = true;
        }

        _ = SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0,
            SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZorder);
    }

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZorder = 0x0200;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
