using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Floaticon.Services;

namespace Floaticon.ViewModels;

/// <summary>
/// 写操作确认卡（write_file 等破坏性动作写入前的人工放行）的 ViewModel。
/// 持有一次 <see cref="ConfirmationRequest"/>，负责展示标题 / 目标路径 / 内容预览，
/// 并提供「同意 / 拒绝」命令：点击后经 <see cref="ConfirmationRequest.Complete"/>
/// 放行或拦截挂起的工具线程，按钮随之禁用并显示结果文本。
/// </summary>
public partial class ConfirmationCardViewModel : ObservableObject
{
    /// <summary>关联的确认请求（工具线程侧的挂起点）。</summary>
    public ConfirmationRequest Request { get; }

    /// <summary>确认卡标题（如「覆盖文件」「新建文件」）。</summary>
    public string Title => Request.Title;

    /// <summary>目标文件完整路径。</summary>
    public string TargetPath => Request.Detail;

    /// <summary>将写入内容的预览。</summary>
    public string? Preview => Request.Preview;

    /// <summary>是否还在等待用户决定（决定后按钮隐藏、显示结果文本）。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand))]
    [NotifyCanExecuteChangedFor(nameof(DenyCommand))]
    private bool _isPending = true;

    /// <summary>决定结果文本（如「✅ 已同意」）。</summary>
    [ObservableProperty]
    private string? _outcome;

    public ConfirmationCardViewModel(ConfirmationRequest request) => Request = request;

    private bool CanDecide() => IsPending;

    /// <summary>「同意」：放行挂起的写操作。</summary>
    [RelayCommand(CanExecute = nameof(CanDecide))]
    private void Approve()
    {
        IsPending = false;
        Outcome = "✅ 已同意，工具继续执行";
        Request.Complete(approved: true);
    }

    /// <summary>「拒绝」：拦截挂起的写操作（工具将收到拒绝并如实告知模型）。</summary>
    [RelayCommand(CanExecute = nameof(CanDecide))]
    private void Deny()
    {
        IsPending = false;
        Outcome = "❌ 已拒绝，未写入任何内容";
        Request.Complete(approved: false);
    }
}
