using System.Windows.Controls;

namespace Floaticon.Views;

/// <summary>
/// 写操作确认卡控件：展示待写入内容并让用户同意 / 拒绝。
/// DataContext 必须是 <see cref="Floaticon.ViewModels.ConfirmationCardViewModel"/>。
/// </summary>
public partial class WriteConfirmationCard : UserControl
{
    public WriteConfirmationCard()
    {
        InitializeComponent();
    }
}
