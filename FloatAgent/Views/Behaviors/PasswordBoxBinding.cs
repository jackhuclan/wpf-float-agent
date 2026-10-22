using System.Windows;
using System.Windows.Controls;

namespace Floaticon.Views.Behaviors;

/// <summary>
/// PasswordBox 的密码无法直接绑定（Password 不是依赖属性），
/// 通过该附加属性实现与 ViewModel 的双向同步，供掩码输入使用。
/// </summary>
public static class PasswordBoxBinding
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    // 防止 VM → 控件回写时再次触发 PasswordChanged 造成递归
    private static readonly DependencyProperty SyncingProperty =
        DependencyProperty.RegisterAttached("Syncing", typeof(bool), typeof(PasswordBoxBinding), new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject obj) => (string)obj.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject obj, string value) => obj.SetValue(BoundPasswordProperty, value);

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        // 绑定首次生效时挂上监听；此后保持挂接状态
        box.PasswordChanged -= OnPasswordChanged;
        box.PasswordChanged += OnPasswordChanged;

        // 控件 → VM 的同步会回调这里，此时跳过回写，避免光标重置
        if (!(bool)box.GetValue(SyncingProperty))
        {
            var value = e.NewValue as string ?? string.Empty;
            if (box.Password != value)
            {
                box.Password = value;
            }
        }
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.SetValue(SyncingProperty, true);
        SetBoundPassword(box, box.Password);
        box.SetValue(SyncingProperty, false);
    }
}
