using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.Shared;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;
using Microsoft.Win32;

namespace DocMgr.ViewModels.NetworkTransfer;

/// <summary>
/// 入网编辑弹窗：审批工作台、附件分区与五按钮规则（对齐资料登记 YA-REG-Ed）。
/// </summary>
public sealed partial class NetworkInboundEditDialogViewModel
{
    private bool _isDialogMode = true;
    private bool _attachmentsMeetMandatoryRequirements;
    private string _attachmentRequirementHint = string.Empty;
    private bool _canApproveProd;
    private bool _canApproveRnd;
    private bool _canApproveDeputy;
    private bool _canUpload;

    public ObservableCollection<SystemAttachment> SignedHandoverAttachments { get; } = new();
    public ObservableCollection<SystemAttachment> MaterialPhotoAttachments { get; } = new();
    public ObservableCollection<SystemAttachment> ProofMaterialAttachments { get; } = new();
    public ObservableCollection<SystemAttachment> OtherAttachments { get; } = new();

    /// <summary>与 YA-REG-Ed 同名：签批交接单分区。</summary>
    public ObservableCollection<SystemAttachment> SignedFormAttachments => SignedHandoverAttachments;

    public bool ShowApprovalWorkflowPanel =>
        _mode == NetworkTransferWorkspaceMode.Approval;

    public bool ShowApprovalPanel => ShowApprovalWorkflowPanel;

    public bool ShowApplicationSubmitActions =>
        _mode == NetworkTransferWorkspaceMode.Application;

    public bool ShowApplicationActions => ShowApplicationSubmitActions;

    public bool ShowApprovalActions => ShowApprovalWorkflowPanel;

    public bool IsDialogMode
    {
        get => _isDialogMode;
        private set => SetProperty(ref _isDialogMode, value);
    }

    public bool ShowEmbeddedActionButtons => !IsDialogMode;

    public bool ShowFooterActionBar => IsDialogMode;

    /// <summary>顶部流程说明（与 YA-REG-Ed <see cref="RegisterWorkspaceBannerText"/> 同构）。</summary>
    public string RegisterWorkspaceBannerText => _mode switch
    {
        NetworkTransferWorkspaceMode.Application =>
            "请填写入网资料与明细，可使用「保存草稿」「提交申请」。提交后由资料室在「入网审批」办理。",
        NetworkTransferWorkspaceMode.Approval =>
            "请先根据线下审批结果核实并登记各明细密级，再填写审批流程，按“审批通过→确认实物交接→上传签批交接单→确认办结→打印交接单”办理。",
        _ => BannerText
    };

    public string WorkspaceBannerText => RegisterWorkspaceBannerText;

    public string? SelectedProjectYear
    {
        get => Year;
        set => Year = value ?? string.Empty;
    }

    public string SourceKindDisplay =>
        NetworkTransferDomainValues.NormalizeSourceKind(_record.SourceKind);

    public string ProofMaterialDisplay =>
        HasProofMaterial
            ? (string.IsNullOrWhiteSpace(_record.ProofMaterialNote)
                ? "-"
                : _record.ProofMaterialNote.Trim())
            : ArchiveRegisterDomainValues.ProofMaterialNoneText;

    public bool CanApproveProd
    {
        get => _canApproveProd;
        private set => SetProperty(ref _canApproveProd, value);
    }

    public bool CanApproveRnd
    {
        get => _canApproveRnd;
        private set => SetProperty(ref _canApproveRnd, value);
    }

    public bool CanApproveDeputy
    {
        get => _canApproveDeputy;
        private set => SetProperty(ref _canApproveDeputy, value);
    }

    public bool CanUpload
    {
        get => _canUpload;
        private set => SetProperty(ref _canUpload, value);
    }

    /// <summary>申请与审批补录阶段均允许编辑明细密级。</summary>
    public bool CanEditItemConfidentialLevel =>
        CanEditForm
        || (_mode == NetworkTransferWorkspaceMode.Approval
            && _record.Status == NetworkInboundRecord.StatusSubmitted
            && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser));

    public bool CanApprovePass => ResolveApprovalButtonState().CanApprovePass;

    public bool CanConfirmPhysicalHandover => ResolveApprovalButtonState().CanConfirmPhysicalHandover;

    public bool CanUploadSignedAttachment => ResolveApprovalButtonState().CanUploadSignedAttachment;

    public bool CanCompleteApproval => ResolveApprovalButtonState().CanConfirmComplete;

    public bool CanPrintHandoverSheet => ResolveApprovalButtonState().CanPrintHandoverSheet;

    public bool CanApprove => CanApprovePass;
    public bool CanConfirmHandover => CanConfirmPhysicalHandover;
    public bool CanUploadAttachment => CanUploadSignedAttachment;
    public bool CanComplete => CanCompleteApproval;

    public bool AttachmentsMeetMandatoryRequirements
    {
        get => _attachmentsMeetMandatoryRequirements;
        private set => SetProperty(ref _attachmentsMeetMandatoryRequirements, value);
    }

    public string AttachmentRequirementHint
    {
        get => _attachmentRequirementHint;
        private set => SetProperty(ref _attachmentRequirementHint, value);
    }

    public string ApproveHintText => CanApprovePass
        ? "请先根据线下审批结果核实各明细密级，再执行审批通过；可补录服务器路径、资料路径与借出硬盘归位档口。"
        : "仅「已提交」状态可执行审批通过。";

    public string ConfirmHandoverHintText => CanConfirmPhysicalHandover
        ? "请核实移交人、资料员、部门负责人签字后确认实物交接。"
        : "请先执行「审批通过」。";

    public string UploadHintText => CanUploadSignedAttachment
        ? "请在「附件材料」分区分别上传签批交接单"
            + (RequiresProofMaterialScanUpload ? "与证明材料" : string.Empty)
            + "（格式限 PDF/图像）。"
        : "请先执行「审批通过」并确认实物交接。";

    public bool RequiresProofMaterialScanUpload =>
        ArchiveRegisterDomainValues.RequiresProofMaterialAttachment(_record.ProofMaterialNote);

    public string ProofMaterialAttachmentHint => RequiresProofMaterialScanUpload
        ? "申请时已声明有证明材料，须上传扫描件后方可办结。"
        : "申请时未声明证明材料，本区可不上传。";

    public bool CanUploadProofMaterialAttachment =>
        CanUploadSignedAttachment && RequiresProofMaterialScanUpload;

    public string CompleteHintText => CanCompleteApproval
        ? "确认办结后写入在网台账，可打印交接单。"
        : (RequiresProofMaterialScanUpload
            ? "请先在附件区上传签批交接单及证明材料后再确认办结。"
            : "请先在附件区上传签批交接单后再确认办结。");

    public string PrintHintText => CanPrintHandoverSheet
        ? (_record.Status >= NetworkInboundRecord.StatusCompleted
            ? "流程已办结，可打印交接单。"
            : "可打印当前申请单。")
        : "请先完成「确认办结」。";

    public RelayCommand SaveApprovalCommand { get; private set; } = null!;
    public RelayCommand ConfirmPhysicalHandoverCommand { get; private set; } = null!;
    public RelayCommand PrintApprovalCommand { get; private set; } = null!;
    public RelayCommand SubmitApplicationsCommand { get; private set; } = null!;
    public RelayCommand PrintApplicationsCommand { get; private set; } = null!;
    public RelayCommand UploadSignedHandoverAttachmentCommand { get; private set; } = null!;
    public RelayCommand UploadSignedFormAttachmentCommand { get; private set; } = null!;
    public RelayCommand UploadMaterialPhotoAttachmentCommand { get; private set; } = null!;
    public RelayCommand UploadProofMaterialAttachmentCommand { get; private set; } = null!;
    public RelayCommand UploadOtherAttachmentCommand { get; private set; } = null!;
    public RelayCommand CaptureSignedHandoverAttachmentCommand { get; private set; } = null!;
    public RelayCommand CaptureProofMaterialAttachmentCommand { get; private set; } = null!;
    public RelayCommand CaptureOtherAttachmentCommand { get; private set; } = null!;
    public RelayCommand FillDefaultApprovalInfoCommand { get; private set; } = null!;

    public void SetDialogMode(bool isDialogMode)
    {
        if (IsDialogMode == isDialogMode)
        {
            return;
        }

        IsDialogMode = isDialogMode;
        OnPropertyChanged(nameof(ShowEmbeddedActionButtons));
        OnPropertyChanged(nameof(ShowFooterActionBar));
        CommandManager.InvalidateRequerySuggested();
    }

    private void InitializeApprovalCommands()
    {
        SaveApprovalCommand = new RelayCommand(async _ => await ApproveAsync(), _ => CanApprovePass);
        ConfirmPhysicalHandoverCommand = new RelayCommand(async _ => await ConfirmHandoverAsync(), _ => CanConfirmPhysicalHandover);
        PrintApprovalCommand = new RelayCommand(async _ => await PrintApprovalAsync(), _ => CanPrintHandoverSheet);
        SubmitApplicationsCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit);
        PrintApplicationsCommand = new RelayCommand(async _ => await PrintApplicationAsync(), _ => CanPrintApplication);

        UploadSignedHandoverAttachmentCommand = new RelayCommand(
            async _ => await UploadAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategorySignedForm),
            _ => CanUploadSignedAttachment);
        UploadSignedFormAttachmentCommand = UploadSignedHandoverAttachmentCommand;
        UploadMaterialPhotoAttachmentCommand = new RelayCommand(_ => { }, _ => false);
        UploadProofMaterialAttachmentCommand = new RelayCommand(
            async _ => await UploadAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategoryProofMaterial),
            _ => CanUploadProofMaterialAttachment);
        UploadOtherAttachmentCommand = new RelayCommand(
            async _ => await UploadAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategoryOther),
            _ => CanUploadSignedAttachment);
        CaptureSignedHandoverAttachmentCommand = new RelayCommand(
            async _ => await CaptureAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategorySignedForm),
            _ => CanUploadSignedAttachment);
        CaptureProofMaterialAttachmentCommand = new RelayCommand(
            async _ => await CaptureAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategoryProofMaterial),
            _ => CanUploadProofMaterialAttachment);
        CaptureOtherAttachmentCommand = new RelayCommand(
            async _ => await CaptureAttachmentByCategoryAsync(NetworkTransferDomainValues.AttachmentCategoryOther),
            _ => CanUploadSignedAttachment);
        FillDefaultApprovalInfoCommand = new RelayCommand(
            async _ => await FillDefaultApprovalInfoAsync(),
            _ => CanApproveProd && _record.Id > 0);
    }

    private async Task FillDefaultApprovalInfoAsync()
    {
        var user = _userContextService.CurrentUser;
        if (user == null)
        {
            return;
        }

        try
        {
            await _archiveRegisterService.ApplyDefaultInboundApprovalInfoAsync(_record, user);
            SyncApprovalFieldsFromRecord();
            OnPropertyChanged(nameof(CurrentRecord));
            _dialogService.ShowMessage("已回填默认审批信息。");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("回填默认审批信息失败：" + ex.Message);
        }
    }

    private NetworkInboundApprovalButtonSupport.ButtonState ResolveApprovalButtonState()
    {
        if (_mode != NetworkTransferWorkspaceMode.Approval)
        {
            return new NetworkInboundApprovalButtonSupport.ButtonState(
                new ApprovalWorkflowButtonSupport.ButtonState(false, false, false, false, false));
        }

        bool isOperatorAllowed = ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        bool canExecuteApprovePass = CanApproveProd && _record.Status == NetworkInboundRecord.StatusSubmitted;
        return NetworkInboundApprovalButtonSupport.Resolve(
            _record,
            isOperatorAllowed,
            canExecuteApprovePass,
            AttachmentsMeetMandatoryRequirements);
    }

    private ApprovalWorkflowButtonSupport.Phase ResolveApprovalPhase() =>
        NetworkInboundApprovalButtonSupport.ResolvePhase(_record, AttachmentsMeetMandatoryRequirements);

    private void UpdateApprovalUiState()
    {
        bool isArchiveAdmin = ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        CanApproveProd = ShowApprovalWorkflowPanel
            && _record.Status == NetworkInboundRecord.StatusSubmitted
            && isArchiveAdmin;
        CanApproveRnd = CanApproveProd;
        CanApproveDeputy = CanApproveProd;
        CanUpload = ShowApprovalWorkflowPanel
            && _record.Status is NetworkInboundRecord.StatusApproved
                or NetworkInboundRecord.StatusSignedUploaded
            && isArchiveAdmin;

        OnPropertyChanged(nameof(ShowApprovalWorkflowPanel));
        OnPropertyChanged(nameof(ShowApprovalPanel));
        OnPropertyChanged(nameof(ShowApplicationSubmitActions));
        OnPropertyChanged(nameof(ShowApplicationActions));
        OnPropertyChanged(nameof(ShowApprovalActions));
        OnPropertyChanged(nameof(RegisterWorkspaceBannerText));
        OnPropertyChanged(nameof(WorkspaceBannerText));
        OnPropertyChanged(nameof(CanEditItemConfidentialLevel));
        OnPropertyChanged(nameof(CanEditApprovalPaths));
        OnPropertyChanged(nameof(CanEditItemPaths));
        OnPropertyChanged(nameof(CanEditServerPath));
        OnPropertyChanged(nameof(IsItemPathReadOnly));
        OnPropertyChanged(nameof(CanApprovePass));
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanConfirmPhysicalHandover));
        OnPropertyChanged(nameof(CanConfirmHandover));
        OnPropertyChanged(nameof(CanUploadSignedAttachment));
        OnPropertyChanged(nameof(CanUploadAttachment));
        OnPropertyChanged(nameof(CanCompleteApproval));
        OnPropertyChanged(nameof(CanComplete));
        OnPropertyChanged(nameof(CanPrintHandoverSheet));
        OnPropertyChanged(nameof(ApproveHintText));
        OnPropertyChanged(nameof(ConfirmHandoverHintText));
        OnPropertyChanged(nameof(UploadHintText));
        OnPropertyChanged(nameof(CompleteHintText));
        OnPropertyChanged(nameof(PrintHintText));
        OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
        OnPropertyChanged(nameof(ProofMaterialAttachmentHint));
        OnPropertyChanged(nameof(CanUploadProofMaterialAttachment));
        OnPropertyChanged(nameof(SourceKindDisplay));
        OnPropertyChanged(nameof(ProvideUnitDisplay));
        OnPropertyChanged(nameof(ProofMaterialDisplay));
        OnPropertyChanged(nameof(SelectedProjectYear));
        OnPropertyChanged(nameof(IsInboundItemGridReadOnly));
        SyncElectronicMediaEditorEditState();
        CommandManager.InvalidateRequerySuggested();
    }

    private void RedistributeAttachmentsByCategory()
    {
        SignedHandoverAttachments.Clear();
        MaterialPhotoAttachments.Clear();
        ProofMaterialAttachments.Clear();
        OtherAttachments.Clear();

        foreach (SystemAttachment attachment in Attachments)
        {
            string category = attachment.FileCategory?.Trim() ?? string.Empty;
            if (string.Equals(category, NetworkTransferDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal))
            {
                SignedHandoverAttachments.Add(attachment);
            }
            else if (string.Equals(category, NetworkTransferDomainValues.AttachmentCategoryProofMaterial, StringComparison.Ordinal))
            {
                ProofMaterialAttachments.Add(attachment);
            }
            else
            {
                OtherAttachments.Add(attachment);
            }
        }
    }

    private Task RefreshAttachmentRequirementsAsync()
    {
        if (!ShowApprovalWorkflowPanel || _record.Status < NetworkInboundRecord.StatusSignedUploaded)
        {
            AttachmentsMeetMandatoryRequirements = true;
            AttachmentRequirementHint = string.Empty;
            UpdateApprovalUiState();
            return Task.CompletedTask;
        }

        IReadOnlyList<string> errors = NetworkInboundApplicationValidationSupport.ValidateForComplete(
            _record,
            Attachments.ToList());
        AttachmentsMeetMandatoryRequirements = errors.Count == 0;
        AttachmentRequirementHint = errors.Count == 0
            ? (RequiresProofMaterialScanUpload
                ? "必备附件已齐全：签批交接单、证明材料。"
                : "必备附件已齐全：签批交接单。")
            : "必备附件未齐全：\n" + string.Join(Environment.NewLine, errors);

        UpdateApprovalUiState();
        return Task.CompletedTask;
    }

    private async Task UploadAttachmentByCategoryAsync(string fileCategory)
    {
        if (string.IsNullOrWhiteSpace(_record.InboundNo))
        {
            _dialogService.ShowMessage("请先保存草稿以生成入网单编号。");
            return;
        }

        if (!CanUploadSignedAttachment)
        {
            _dialogService.ShowMessage(UploadHintText);
            return;
        }

        if (string.Equals(fileCategory, NetworkTransferDomainValues.AttachmentCategoryProofMaterial, StringComparison.Ordinal)
            && !CanUploadProofMaterialAttachment)
        {
            _dialogService.ShowMessage("申请时未声明附有证明材料，无需上传证明材料扫描件。");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Title = $"请选择附件（{SystemAttachmentUploadSupport.AllowedFormatsDescription}）",
            Filter = SystemAttachmentUploadSupport.OpenFileDialogFilter
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        bool anySuccess = false;
        foreach (string filePath in dialog.FileNames)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    continue;
                }

                byte[] content = await File.ReadAllBytesAsync(filePath);
                string fileName = Path.GetFileName(filePath);
                string extension = Path.GetExtension(filePath);
                var (ok, message, _) = await _service.UploadAttachmentAsync(
                    NetworkTransferDomainValues.InboundAttachmentBusinessType,
                    _record.Id,
                    _record.InboundNo,
                    fileCategory,
                    fileName,
                    extension,
                    content.LongLength,
                    content,
                    RequireUser());
                if (!ok)
                {
                    _dialogService.ShowError(message);
                    continue;
                }

                anySuccess = true;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"上传失败：{ex.Message}");
            }
        }

        if (!anySuccess)
        {
            return;
        }

        _hasCommittedChanges = true;
        await ReloadAttachmentsAsync();
        await ReloadRecordAsync();

        string displayName = string.Equals(fileCategory, NetworkTransferDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal)
            ? "签批交接单"
            : fileCategory;
        if (!AttachmentsMeetMandatoryRequirements)
        {
            _dialogService.ShowMessage($"{displayName}已上传。\n\n当前尚未满足办结要求：\n{AttachmentRequirementHint}");
        }
    }

    private async Task CaptureAttachmentByCategoryAsync(string fileCategory)
    {
        if (string.IsNullOrWhiteSpace(_record.InboundNo))
        {
            _dialogService.ShowMessage("请先保存草稿以生成入网单编号。");
            return;
        }

        if (!CanUploadSignedAttachment)
        {
            _dialogService.ShowMessage(UploadHintText);
            return;
        }

        if (string.Equals(fileCategory, NetworkTransferDomainValues.AttachmentCategoryProofMaterial, StringComparison.Ordinal)
            && !CanUploadProofMaterialAttachment)
        {
            _dialogService.ShowMessage("申请时未声明附有证明材料，无需上传证明材料扫描件。");
            return;
        }

        DocumentCameraCaptureResult? captured = DocumentCameraAttachmentCaptureSupport.Capture(_dialogService);
        if (captured == null)
        {
            return;
        }

        string displayName = string.Equals(fileCategory, NetworkTransferDomainValues.AttachmentCategorySignedForm, StringComparison.Ordinal)
            ? "签批交接单"
            : fileCategory;
        string fileName = DocumentCameraAttachmentCaptureSupport.BuildFileName(_record.InboundNo, displayName, "入网");
        try
        {
            var (ok, message, _) = await _service.UploadAttachmentAsync(
                NetworkTransferDomainValues.InboundAttachmentBusinessType,
                _record.Id,
                _record.InboundNo,
                fileCategory,
                fileName,
                ".jpg",
                captured.JpegContent.LongLength,
                captured.JpegContent,
                RequireUser());
            if (!ok)
            {
                _dialogService.ShowError(message);
                return;
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"上传失败：{ex.Message}");
            return;
        }

        _hasCommittedChanges = true;
        await ReloadAttachmentsAsync();
        await ReloadRecordAsync();

        if (!AttachmentsMeetMandatoryRequirements)
        {
            _dialogService.ShowMessage($"{displayName}已上传。\n\n当前尚未满足办结要求：\n{AttachmentRequirementHint}");
        }
    }

    private async Task PrintApprovalAsync()
    {
        try
        {
            bool blankApproval = _record.Status < NetworkInboundRecord.StatusCompleted;
            var data = await _service.BuildInboundPrintDataAsync(_record.Id, blankApproval);
            var document = NetworkInboundPrintDocumentFactory.Create(data);
            var previewWindow = new PrintPreviewWindow(document)
            {
                Owner = Application.Current.MainWindow
            };

            await _service.RecordInboundPrintAsync(_record.Id);
            previewWindow.ShowDialog();
            await ReloadRecordAsync();
        }
        catch (InvalidOperationException ex)
        {
            _dialogService.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("打印生成失败：" + ex.Message);
        }
    }
}
