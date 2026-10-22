using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Floaticon.Views.Behaviors;

/// <summary>
/// 让一组窗口"始终在最上面"，并保持它们彼此的先后次序稳定。
///
/// 为什么不能每个窗口各管各的：
/// <c>SetWindowPos(hwnd, HWND_TOPMOST, ...)</c> 会把窗口顶到置顶层的最前面。若悬浮球、
/// 聊天窗、配置窗各自跑一个定时器，它们就会互相揭顶——每 1.5 秒轮转一次，
/// 表现就是配置窗反复"消失又出现"、无法连续输入。
///
/// 因此这里只维护一个全局定时器，一次遍历把所有受管窗口按 <see cref="LayerProperty"/>
/// 从下往上依次摆放（第一个 HWND_TOPMOST，后续依次"插到前一个之上"），
/// 顺序恒定：悬浮球 ⊂ 聊天窗 ⊂ 配置窗。全程 SWP_NOACTIVATE，不抢焦点。
/// </summary>
public static class TopmostKeeper
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(1200);

    /// <summary>受管窗口，按注册顺序（Layer 相同时后注册者更靠前）。</summary>
    private static readonly List<Window> Managed = new();

    private static DispatcherTimer? _timer;
    private static bool _applying;

    public static readonly DependencyProperty KeepTopmostProperty =
        DependencyProperty.RegisterAttached(
            "KeepTopmost", typeof(bool), typeof(TopmostKeeper),
            new PropertyMetadata(false, OnKeepTopmostChanged));

    /// <summary>层级：数值越大越靠前。默认 0。</summary>
    public static readonly DependencyProperty LayerProperty =
        DependencyProperty.RegisterAttached("Layer", typeof(int), typeof(TopmostKeeper), new PropertyMetadata(0));

    [AttachedPropertyBrowsableForType(typeof(Window))]
    public static bool GetKeepTopmost(DependencyObject obj) => (bool)obj.GetValue(KeepTopmostProperty);

    public static void SetKeepTopmost(DependencyObject obj, bool value) => obj.SetValue(KeepTopmostProperty, value);

    public static int GetLayer(DependencyObject obj) => (int)obj.GetValue(LayerProperty);

    public static void SetLayer(DependencyObject obj, int value) => obj.SetValue(LayerProperty, value);

    private static void OnKeepTopmostChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window win)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Register(win);
        }
        else
        {
            Unregister(win);
        }
    }

    private static void Register(Window win)
    {
        if (Managed.Contains(win))
        {
            return;
        }

        Managed.Add(win);
        win.Loaded += OnLoaded;
        win.Deactivated += OnDeactivated;
        win.IsVisibleChanged += OnIsVisibleChanged;
        win.Closed += OnClosed;

        EnsureTimer(win.Dispatcher);

        if (win.IsLoaded)
        {
            ApplyOrder();
        }
    }

    private static void Unregister(Window win)
    {
        if (!Managed.Remove(win))
        {
            return;
        }

        win.Loaded -= OnLoaded;
        win.Deactivated -= OnDeactivated;
        win.IsVisibleChanged -= OnIsVisibleChanged;
        win.Closed -= OnClosed;

        if (Managed.Count == 0)
        {
            StopTimer();
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => ApplyOrder();

    // 被别的窗口抢走焦点/置顶位时立刻纠正一次，不必等下一个定时器周期
    private static void OnDeactivated(object? sender, EventArgs e) => ApplyOrder();

    // 聊天窗/配置窗显示或隐藏时，次序需要马上重排
    private static void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => ApplyOrder();

    private static void OnClosed(object? sender, EventArgs e) => Unregister((Window)sender!);

    private static void EnsureTimer(Dispatcher dispatcher)
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = Interval };
        _timer.Tick += (_, _) => ApplyOrder();
        _timer.Start();
    }

    private static void StopTimer()
    {
        if (_timer is null)
        {
            return;
        }

        _timer.Stop();
        _timer = null;
    }

    /// <summary>
    /// 一次遍历完成全部窗口的置顶与排序。只在 UI 线程调度器上执行，
    /// 并用 <c>_applying</c> 防重入（SetWindowPos 可能触发窗口事件回调）。
    /// </summary>
    private static void ApplyOrder()
    {
        if (_applying)
        {
            return;
        }

        _applying = true;
        try
        {
            // 从上往下摆（Layer 大的在前）：SetWindowPos 的 hWndInsertAfter 表示
            // "排在这个窗口之后（下方）"，所以先放最上层，再让下一层紧贴它放。
            var ordered = Managed
                .Where(w => w.IsVisible && w.WindowState != WindowState.Minimized)
                .OrderByDescending(GetLayer)
                .ToList();

            var upper = IntPtr.Zero;
            foreach (var win in ordered)
            {
                var hwnd = new WindowInteropHelper(win).EnsureHandle();

                // 最上层直接顶到置顶层最前，其余依次落到"上一层的正下方"
                var insertAfter = upper == IntPtr.Zero ? HwndTopmost : upper;

                _ = SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0,
                    SwpNoSize | SwpNoMove | SwpNoActivate | SwpNoOwnerZorder | SwpNoSendChanging);

                upper = hwnd;
            }
        }
        catch (InvalidOperationException)
        {
            // 关闭流程中窗口句柄可能已失效，忽略即可
        }
        finally
        {
            _applying = false;
        }
    }

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoOwnerZorder = 0x0200;
    private const uint SwpNoSendChanging = 0x0400;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
}
