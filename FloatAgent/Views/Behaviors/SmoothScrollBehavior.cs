using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Floaticon.Views.Behaviors;

/// <summary>
/// 给 ScrollViewer 的滚轮滚动加上缓动。
/// WPF 原生滚轮是"一次跳 3 行"的瞬间位移，观感生硬；这里改为按帧插值滚动。
/// </summary>
public static class SmoothScrollBehavior
{
    /// <summary>一档滚轮（Delta=120）滚动的像素数。</summary>
    private const double PixelsPerNotch = 96;

    /// <summary>一次滚动动画时长（毫秒）。</summary>
    private const double DurationMs = 170;

    private static readonly ConditionalWeakTable<ScrollViewer, AnimationState> States = new();

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(SmoothScrollBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            viewer.PreviewMouseWheel += OnPreviewMouseWheel;
        }
        else
        {
            viewer.PreviewMouseWheel -= OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer || e.Handled || viewer.ScrollableHeight <= 0)
        {
            return;
        }

        e.Handled = true;

        var target = Math.Clamp(
            viewer.VerticalOffset - e.Delta / 120.0 * PixelsPerNotch,
            0,
            viewer.ScrollableHeight);

        Animate(viewer, target);
    }

    private static void Animate(ScrollViewer viewer, double target)
    {
        var state = States.GetOrCreateValue(viewer);
        state.From = viewer.VerticalOffset;
        state.To = target;
        state.StartTicks = Stopwatch.GetTimestamp();

        if (state.Timer is null)
        {
            // 用 Render 优先级，和界面刷新同频，避免重复布局
            state.Timer = new DispatcherTimer(DispatcherPriority.Render, viewer.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(15)
            };
            state.Timer.Tick += (_, _) => Step(viewer, state);
        }

        if (!state.Timer.IsEnabled)
        {
            state.Timer.Start();
        }
    }

    private static void Step(ScrollViewer viewer, AnimationState state)
    {
        var elapsedMs = (Stopwatch.GetTimestamp() - state.StartTicks) * 1000.0 / Stopwatch.Frequency;
        var progress = Math.Clamp(elapsedMs / DurationMs, 0, 1);

        // ease-out cubic
        var eased = 1 - Math.Pow(1 - progress, 3);
        viewer.ScrollToVerticalOffset(state.From + (state.To - state.From) * eased);

        if (progress >= 1 && state.Timer is not null)
        {
            state.Timer.Stop();
        }
    }

    private sealed class AnimationState
    {
        public DispatcherTimer? Timer;
        public double From;
        public double To;
        public long StartTicks;
    }
}
