using System.Windows;
using System.Windows.Controls;

namespace Floaticon.Views.Behaviors;

/// <summary>
/// PasswordBox 的 Password 不是依赖属性、无法直接绑定，
/// 这里通过附加属性实现与 ViewModel 的双向同步，供掩码输入使用。
///
/// 订阅方式用类处理器（EventManager.RegisterClassHandler），而不是在 DP 值变化回调里挂
/// PasswordChanged：绑定初次附加时若 VM 初始值恰好等于 DP 默认值（如都是空字符串），
/// 值不会发生变化、回调不触发，监听就永远挂不上——表现为输入的内容传不回 ViewModel，
/// 红框不消失、保存时仍判为空。
///
/// 控件 → ViewModel 的方向必须用 <c>SetCurrentValue</c> + <c>UpdateSource</c>，
/// 不能用 <c>SetValue</c>——对已绑定属性调用 SetValue 会以本地值覆盖掉绑定表达式，
/// 绑定从此失效。
/// </summary>
public static class PasswordBoxBinding
{
    static PasswordBoxBinding()
    {
        // 所有 PasswordBox 的密码变化都先进这里；未挂 BoundPassword 绑定的框直接跳过
        EventManager.RegisterClassHandler(
            typeof(PasswordBox),
            PasswordBox.PasswordChangedEvent,
            new RoutedEventHandler(OnPasswordChanged));
    }

    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword", typeof(string), typeof(PasswordBoxBinding),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

    // 防递归：ViewModel → 控件 回写时，控件抛出的 PasswordChanged 不再回推给 ViewModel
    private static readonly DependencyProperty SyncingProperty =
        DependencyProperty.RegisterAttached("Syncing", typeof(bool), typeof(PasswordBoxBinding), new PropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject obj) => (string)obj.GetValue(BoundPasswordProperty);
    public static void SetBoundPassword(DependencyObject obj, string value) => obj.SetValue(BoundPasswordProperty, value);

    /// <summary>ViewModel → 控件：外部赋值（打开窗口回填等）时同步到 PasswordBox。</summary>
    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box)
        {
            return;
        }

        // 来自控件自身（控件 → VM 的回流），无需回写，否则光标会跳到开头
        if (box.GetValue(SyncingProperty) is true)
        {
            return;
        }

        var value = e.NewValue as string ?? string.Empty;
        if (box.Password == value)
        {
            return;
        }

        box.SetValue(SyncingProperty, true);
        try
        {
            box.Password = value;
        }
        finally
        {
            box.SetValue(SyncingProperty, false);
        }
    }

    /// <summary>控件 → ViewModel：更新附加属性并显式把值推回绑定源。</summary>
    private static void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox box || box.GetValue(SyncingProperty) is true)
        {
            return;
        }

        // 只处理挂了本绑定方案的框
        if (box.GetBindingExpression(BoundPasswordProperty) is null)
        {
            return;
        }

        box.SetValue(SyncingProperty, true);
        try
        {
            // SetCurrentValue 保留绑定；随后的 UpdateSource 保证 ViewModel 立刻拿到新值
            box.SetCurrentValue(BoundPasswordProperty, box.Password);
            box.GetBindingExpression(BoundPasswordProperty)?.UpdateSource();
        }
        finally
        {
            box.SetValue(SyncingProperty, false);
        }
    }
}
