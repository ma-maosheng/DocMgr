using System.Windows.Input;

using DocMgr.ViewModels.Base;

namespace DocMgr.ViewModels.Shared;

/// <summary>
/// 建档申请与入网申请共用的申请信息头。
/// 业务差异（资料名称、库管模式、入网数据来源等）由宿主通过内容槽挂入，不写入本模型。
/// </summary>
public sealed class ApplicationRequestHeaderModel : ViewModelBase
{
    private string _applicantName = string.Empty;
    private string _applicantDept = string.Empty;
    private DateTime? _applicationDate;
    private string _applicationReason = string.Empty;
    private string _otherRequests = string.Empty;
    private bool _hasProofMaterial;
    private string _proofMaterialName = string.Empty;
    private bool _canEdit;
    private bool _canEditApplicationDate;
    private bool _showSubmitActions;
    private bool _showEmbeddedActionButtons;
    private ICommand? _saveDraftCommand;
    private ICommand? _submitCommand;
    private ICommand? _printCommand;

    /// <summary>申请人。</summary>
    public string ApplicantName
    {
        get => _applicantName;
        set => SetProperty(ref _applicantName, value ?? string.Empty);
    }

    /// <summary>申请部门。</summary>
    public string ApplicantDept
    {
        get => _applicantDept;
        set => SetProperty(ref _applicantDept, value ?? string.Empty);
    }

    /// <summary>申请日期。</summary>
    public DateTime? ApplicationDate
    {
        get => _applicationDate;
        set => SetProperty(ref _applicationDate, value);
    }

    /// <summary>申请说明。</summary>
    public string ApplicationReason
    {
        get => _applicationReason;
        set => SetProperty(ref _applicationReason, value ?? string.Empty);
    }

    /// <summary>其他要求。</summary>
    public string OtherRequests
    {
        get => _otherRequests;
        set => SetProperty(ref _otherRequests, value ?? string.Empty);
    }

    /// <summary>是否附有证明材料。</summary>
    public bool HasProofMaterial
    {
        get => _hasProofMaterial;
        set => SetProperty(ref _hasProofMaterial, value);
    }

    /// <summary>证明材料名称。</summary>
    public string ProofMaterialName
    {
        get => _proofMaterialName;
        set => SetProperty(ref _proofMaterialName, value ?? string.Empty);
    }

    /// <summary>申请说明、其他要求与证明材料是否可编辑。</summary>
    public bool CanEdit
    {
        get => _canEdit;
        set => SetProperty(ref _canEdit, value);
    }

    /// <summary>申请日期是否可改。入网申请的日期由系统写入，保持只读。</summary>
    public bool CanEditApplicationDate
    {
        get => _canEditApplicationDate;
        set => SetProperty(ref _canEditApplicationDate, value);
    }

    /// <summary>是否处于申请工作台。</summary>
    public bool ShowSubmitActions
    {
        get => _showSubmitActions;
        set => SetProperty(ref _showSubmitActions, value);
    }

    /// <summary>非独立弹窗时，把保存/提交/打印放在表头右侧。</summary>
    public bool ShowEmbeddedActionButtons
    {
        get => _showEmbeddedActionButtons;
        set => SetProperty(ref _showEmbeddedActionButtons, value);
    }

    /// <summary>保存草稿。</summary>
    public ICommand? SaveDraftCommand
    {
        get => _saveDraftCommand;
        set => SetProperty(ref _saveDraftCommand, value);
    }

    /// <summary>提交申请。</summary>
    public ICommand? SubmitCommand
    {
        get => _submitCommand;
        set => SetProperty(ref _submitCommand, value);
    }

    /// <summary>打印申请。</summary>
    public ICommand? PrintCommand
    {
        get => _printCommand;
        set => SetProperty(ref _printCommand, value);
    }
}
