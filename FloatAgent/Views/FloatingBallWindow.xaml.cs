using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Floaticon.Services;

namespace Floaticon.Views;

/// <summary>
/// 悬浮 AI 助手球：置顶、无边框、可拖拽、点击切换聊天窗。
/// </summary>
public partial class FloatingBallWindow : Window
{
    // 判定"点击"而非"拖拽"的最大位移（DIP）
    private const double ClickThreshold = 4.0;

    private readonly ChatWindow _chatWindow;

    public FloatingBallWindow(ChatWindow chatWindow)
    {
        InitializeComponent();
        _chatWindow = chatWindow;
    }

    /// <summary>启动时放到屏幕工作区右下角。</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - 24;
        Top = work.Bottom - Height - 24;
    }

    private void OnBallMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var leftBefore = Left;
        var topBefore = Top;

        // DragMove 阻塞直到松开鼠标；返回后根据窗口是否移动区分点击/拖拽
        DragMove();

        var moved = System.Math.Abs(Left - leftBefore) > ClickThreshold
                    || System.Math.Abs(Top - topBefore) > ClickThreshold;

        if (!moved)
        {
            ToggleChat();
        }
        else
        {
            // 拖动后聊天窗若开着，跟随到球上方
            if (_chatWindow.IsVisible)
            {
                _chatWindow.ShowAbove(BallBounds);
            }
        }
    }

    /// <summary>悬浮球在屏幕坐标系中的矩形。</summary>
    public Rect BallBounds => new(Left, Top, ActualWidth, ActualHeight);

    private void ToggleChat()
    {
        if (_chatWindow.IsVisible)
        {
            _chatWindow.Hide();
        }
        else
        {
            _chatWindow.ShowAbove(BallBounds);
            PlayPopAnimation();
        }
    }

    private void OnOpenChatMenuClick(object sender, RoutedEventArgs e)
    {
        if (!_chatWindow.IsVisible)
        {
            _chatWindow.ShowAbove(BallBounds);
        }
    }

    private void OnExitMenuClick(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    /// <summary>弹出聊天窗时球做个弹性缩放反馈。</summary>
    private void PlayPopAnimation()
    {
        var anim = new DoubleAnimation
        {
            From = 0.85,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.6 }
        };
        BallScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        BallScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }
}
