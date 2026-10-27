using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Floaticon.Services;
using Floaticon.ViewModels;

namespace Floaticon.Views;

/// <summary>
/// 聊天窗口：无边框、圆角、自绘标题栏，弹出时定位在悬浮球正上方。
/// </summary>
public partial class ChatWindow : Window
{
    private const double GapToBall = 10.0;

    /// <summary>距底部这个距离内视为"跟随底部"。</summary>
    private const double FollowThreshold = 96.0;

    /// <summary>抽屉滑入/收起动画时长（FR-3.3：220ms CubicEase InOut）。</summary>
    private static readonly TimeSpan DrawerDuration = TimeSpan.FromMilliseconds(220);

    private bool _drawerOpen;

    public ChatWindow(ChatViewModel viewModel, IConfirmationService confirmation)
    {
        InitializeComponent();
        DataContext = viewModel;
        MessagesList.ItemsSource = viewModel.Messages;

        // 抽屉里打开/新建会话后收起抽屉（FR-3.6 语义：切换即收，避免浮层残留）
        viewModel.History.DrawerCloseRequested += CloseDrawer;

        // 写操作确认请求到达时，聊天窗若被隐藏（收起到悬浮球）则自动弹出并置前，
        // 保证确认卡一定被用户看到
        confirmation.Requested += _ => Dispatcher.Invoke(() =>
        {
            if (!IsVisible)
            {
                Show();
            }

            Activate();
        });

        // 新消息：滚到底部
        ((INotifyCollectionChanged)viewModel.Messages).CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
            {
                foreach (ChatMessage message in e.NewItems)
                {
                    message.PropertyChanged += OnMessagePropertyChanged;
                }
            }

            FollowToBottom(force: true);
        };

        // 流式追加正文时，只要用户没往上翻，就持续跟随
        foreach (var message in viewModel.Messages)
        {
            message.PropertyChanged += OnMessagePropertyChanged;
        }
    }

    private void OnMessagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ChatMessage.Text) or nameof(ChatMessage.IsTyping))
        {
            FollowToBottom(force: false);
        }
    }

    /// <summary>
    /// 跟随到底部。等布局完成后再滚动，避免 ExtentHeight 还是旧值导致滚不到位。
    /// </summary>
    private void FollowToBottom(bool force)
    {
        if (!IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            var atBottom = MessagesScroll.VerticalOffset + MessagesScroll.ViewportHeight
                           >= MessagesScroll.ExtentHeight - FollowThreshold;

            if (force || atBottom)
            {
                MessagesScroll.ScrollToEnd();
            }
        });
    }

    /// <summary>
    /// 在悬浮球正上方显示聊天窗（水平居中于球，并夹取到屏幕工作区内）。
    /// </summary>
    public void ShowAbove(Rect ballBounds)
    {
        var work = SystemParameters.WorkArea;

        Left = ballBounds.Left + ballBounds.Width / 2 - Width / 2;
        Top = ballBounds.Top - Height - GapToBall;

        // 夹取到工作区，避免弹出屏外
        Left = System.Math.Clamp(Left, work.Left + 8, work.Right - Width - 8);
        Top = System.Math.Clamp(Top, work.Top + 8, work.Bottom - Height - 8);

        Show();
        Activate();
        InputBox.Focus();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
        {
            DragMove();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();

    /// <summary>标题栏 ☰：展开/收起会话抽屉（FR-3.2）。</summary>
    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        if (_drawerOpen)
        {
            CloseDrawer();
        }
        else
        {
            OpenDrawer();
        }
    }

    /// <summary>点击遮罩空白处关闭抽屉（FR-3.4）。</summary>
    private void OnDrawerMaskClick(object sender, MouseButtonEventArgs e) => CloseDrawer();

    private void OpenDrawer()
    {
        if (DataContext is not ChatViewModel vm)
        {
            return;
        }

        // 打开时刷新列表（外部新建/删除等变化一并可见）
        vm.History.Refresh(vm.SessionId);

        var width = MessagesHost.ActualWidth / 2;

        DrawerPanel.RenderTransform = new System.Windows.Media.TranslateTransform();
        DrawerOverlay.Visibility = Visibility.Visible;
        DrawerMask.Opacity = 0;

        var slide = new System.Windows.Media.Animation.DoubleAnimation(-width, 0, DrawerDuration)
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut,
            },
        };
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, DrawerDuration);

        DrawerPanel.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        DrawerMask.BeginAnimation(OpacityProperty, fade);
        _drawerOpen = true;
    }

    private void CloseDrawer()
    {
        if (!_drawerOpen || !IsLoaded)
        {
            _drawerOpen = false;
            return;
        }

        var width = Math.Max(MessagesHost.ActualWidth / 2, 1);

        var slide = new System.Windows.Media.Animation.DoubleAnimation(0, -width, DrawerDuration)
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut,
            },
            FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop,
        };
        var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, DrawerDuration);
        slide.Completed += (_, _) => DrawerOverlay.Visibility = Visibility.Collapsed;

        DrawerPanel.RenderTransform?.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
        DrawerMask.BeginAnimation(OpacityProperty, fade);
        _drawerOpen = false;
    }

    /// <summary>打开模型配置对话框（模态），保存后立即生效。</summary>
    private void OnConfigClick(object sender, RoutedEventArgs e)
    {
        var configWindow = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<ConfigWindow>(ServiceLocator.Services);
        configWindow.Owner = this;
        configWindow.ShowDialog();
    }

    /// <summary>
    /// Enter 发送；Shift+Enter 手动插入换行。
    /// 注意：TextBox 的 AcceptsReturn 必须为 False——否则其类处理器会先把 Enter
    /// 标记为已处理并插入换行，这里的实例 KeyDown 处理器根本收不到事件。
    /// </summary>
    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
        {
            // Shift+Enter：在光标处插入换行
            var caret = InputBox.CaretIndex;
            InputBox.Text = InputBox.Text.Insert(caret, "\n");
            InputBox.CaretIndex = caret + 1;
            e.Handled = true;
            return;
        }

        e.Handled = true;

        if (DataContext is ChatViewModel vm && vm.SendCommand.CanExecute(null))
        {
            vm.SendCommand.Execute(null);
        }
    }
}
