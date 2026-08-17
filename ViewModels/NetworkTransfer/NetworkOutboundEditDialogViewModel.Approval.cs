using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using DocMgr.Services.YearlyArchive;
using DocMgr.ViewModels.Base;
using DocMgr.Views.Shared;
using Microsoft.Win32;

namespace DocMgr.ViewModels.NetworkTransfer;

public sealed partial class NetworkOutboundEditDialogViewModel
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
    public ObservableCollection<SystemAttachment> SignedFormAttachments => SignedHandoverAttachments;

    public bool ShowApprovalWorkflowPanel => _mode == NetworkTransferWorkspaceMode.Approval;

    public bool ShowApplicationSubmitActions => _mode == NetworkTransferWorkspaceMode.Application;

    public bool IsDialogMode
    {
        get => _isDialogMode;
        private set => SetProperty(ref _isDialogMode, value);
    }

    public bool ShowEmbeddedActionButtons => !IsDialogMode;

    public string RegisterWorkspaceBannerText => _mode switch
    {
        NetworkTransferWorkspaceMode.Application =>
            "请填写出网资料与电子介质明细，可使用「保存草稿」「提交申请」。拷贝完成后由资料室在「出网审批」补录目录/数据量并办理。",
        NetworkTransferWorkspaceMode.Approval =>
            "请先根据线下审批结果执行审批通过；审批通过后从离线介质补录目录与数据量，再按“确认实物交接→上传签批交接单→确认办结→打印交接单”办理。",
        _ => BannerText
    };

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

    public bool CanApprovePass => ResolveApprovalButtonState().CanApprovePass;
    public bool CanConfirmPhysicalHandover => ResolveApprovalButtonState().CanConfirmPhysicalHandover;
    public bool CanUploadSignedAttachment => ResolveApprovalButtonState().CanUploadSignedAttachment;
    public bool CanCompleteApproval => ResolveApprovalButtonState().CanConfirmComplete;
    public bool CanPrintHandoverSheet => ResolveApprovalButtonState().CanPrintHandoverSheet;
    public bool CanApprove => CanApprovePass;
    public bool CanConfirmHandover => CanConfirmPhysicalHandover;
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
        ? "请核实线下审批结果后执行审批通过；电子介质目录与数据量可在审批通过后补录。"
        : "仅「已提交」状态可执行审批通过。";

    public string ConfirmHandoverHintText => CanConfirmPhysicalHandover
        ? "请先从离线介质补录数据量与目录/文件明细，核实移交人、资料员签字后确认实物交接。"
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
        ? "确认办结前将校验审批交接信息、电子介质数据量与目录/文件个数、必备附件等。"
        : (RequiresProofMaterialScanUpload
            ? "请先在附件区上传签批交接单及证明材料后再确认办结。"
            : "请先在附件区上传签批交接单后再确认办结。");

    public string PrintHintText => CanPrintHandoverSheet
        ? (_record.Status >= NetworkOutboundRecord.StatusCompleted
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
    public RelayCommand FillDefaultApprovalInfoCommand { get; private set; } = null!;

    public void SetDialogMode(bool isDialogMode)
    {
        if (IsDialogMode == isDialogMode)
        {
            return;
        }

        IsDialogMode = isDialogMode;
        OnPropertyChanged(nameof(ShowEmbeddedActionButtons));
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
            await _archiveRegisterService.ApplyDefaultNetworkOutboundApprovalInfoAsync(_record, user);
            SyncApprovalFieldsFromRecord();
            _dialogService.ShowMessage("已回填默认审批信息。");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("回填默认审批信息失败：" + ex.Message);
        }
    }

    private NetworkOutboundApprovalButtonSupport.ButtonState ResolveApprovalButtonState()
    {
        if (_mode != NetworkTransferWorkspaceMode.Approval)
        {
            return new NetworkOutboundApprovalButtonSupport.ButtonState(
                new ApprovalWorkflowButtonSupport.ButtonState(false, false, false, false, false));
        }

        bool isOperatorAllowed = ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        bool canExecuteApprovePass = CanApproveProd && _record.Status == NetworkOutboundRecord.StatusSubmitted;
        return NetworkOutboundApprovalButtonSupport.Resolve(
            _record,
            isOperatorAllowed,
            canExecuteApprovePass,
            AttachmentsMeetMandatoryRequirements);
    }

    private void UpdateApprovalUiState()
    {
        bool isArchiveAdmin = ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser);
        CanApproveProd = ShowApprovalWorkflowPanel
            && _record.Status == NetworkOutboundRecord.StatusSubmitted
            && isArchiveAdmin;
        CanApproveRnd = CanApproveProd;
        CanApproveDeputy = CanApproveProd;
        CanUpload = ShowApprovalWorkflowPanel
            && _record.Status is NetworkOutboundRecord.StatusApproved or NetworkOutboundRecord.StatusSignedUploaded
            && isArchiveAdmin;

        OnPropertyChanged(nameof(ShowApprovalWorkflowPanel));
        OnPropertyChanged(nameof(ShowApplicationSubmitActions));
        OnPropertyChanged(nameof(RegisterWorkspaceBannerText));
        OnPropertyChanged(nameof(CanEditItemConfidentialLevel));
        OnPropertyChanged(nameof(CanEditApprovalPaths));
        OnPropertyChanged(nameof(CanSupplementElectronicContentScan));
        OnPropertyChanged(nameof(CanEditServerPath));
        OnPropertyChanged(nameof(CanApprovePass));
        OnPropertyChanged(nameof(CanConfirmPhysicalHandover));
        OnPropertyChanged(nameof(CanUploadSignedAttachment));
        OnPropertyChanged(nameof(CanCompleteApproval));
        OnPropertyChanged(nameof(CanPrintHandoverSheet));
        OnPropertyChanged(nameof(ApproveHintText));
        OnPropertyChanged(nameof(ConfirmHandoverHintText));
        OnPropertyChanged(nameof(UploadHintText));
        OnPropertyChanged(nameof(CompleteHintText));
        OnPropertyChanged(nameof(PrintHintText));
        OnPropertyChanged(nameof(RequiresProofMaterialScanUpload));
        OnPropertyChanged(nameof(ProofMaterialAttachmentHint));
        OnPropertyChanged(nameof(CanUploadProofMaterialAttachment));
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
        if (!ShowApprovalWorkflowPanel || _record.Status < NetworkOutboundRecord.StatusSignedUploaded)
        {
            AttachmentsMeetMandatoryRequirements = true;
            AttachmentRequirementHint = string.Empty;
            UpdateApprovalUiState();
            return Task.CompletedTask;
        }

        IReadOnlyList<string> errors = CollectCompleteValidationErrors();
        AttachmentsMeetMandatoryRequirements = errors.Count == 0;
        AttachmentRequirementHint = errors.Count == 0
            ? "办结信息已齐全：审批交接、电子介质数据量与目录/文件个数、必备附件均已满足要求。"
            : "办结前信息完整性校验未通过：" + Environment.NewLine + string.Join(Environment.NewLine, errors);
        UpdateApprovalUiState();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 汇总确认实物交接前的校验（含当前界面中的电子介质补录结果）。
    /// </summary>
    private IReadOnlyList<string> CollectHandoverValidationErrors()
    {
        NetworkOutboundRecord validationRecord = BuildCompleteValidationRecord();
        return NetworkOutboundApplicationValidationSupport.ValidateForHandoverConfirm(
            validationRecord,
            validationRecord,
            Attachments.ToList());
    }

    /// <summary>
    /// 汇总确认办结前的完整性校验（含当前界面中的电子介质补录结果）。
    /// </summary>
    private IReadOnlyList<string> CollectCompleteValidationErrors()
    {
        NetworkOutboundRecord validationRecord = BuildCompleteValidationRecord();
        return NetworkOutboundApplicationValidationSupport.ValidateForComplete(
            validationRecord,
            Attachments.ToList());
    }

    private NetworkOutboundRecord BuildCompleteValidationRecord()
    {
        return new NetworkOutboundRecord
        {
            DestinationKind = _record.DestinationKind,
            ServerPath = SelectedServerPath?.PathName ?? _record.ServerPath,
            MaterialPath = _record.MaterialPath,
            ProofMaterialNote = _record.ProofMaterialNote,
            DeptLeader = DeptLeader,
            DeptDate = DeptDate,
            ProdLeader = ProdLeader,
            ProdDate = ProdDate,
            RndLeader = RndLeader,
            RndDate = RndDate,
            DeputyLeader = DeputyLeader,
            DeputyDate = DeputyDate,
            Deliverer = Deliverer,
            DeliverDate = DeliverDate,
            Administrator = Administrator,
            AdminDate = AdminDate,
            MediaEntries = BuildExternalMediaEntriesForSave()
        };
    }

    private async Task UploadAttachmentByCategoryAsync(string fileCategory)
    {
        if (string.IsNullOrWhiteSpace(_record.OutboundNo))
        {
            _dialogService.ShowMessage("请先保存草稿以生成出网单编号。");
            return;
        }

        if (!CanUploadSignedAttachment)
        {
            _dialogService.ShowMessage(UploadHintText);
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
                    NetworkTransferDomainValues.OutboundAttachmentBusinessType,
                    _record.Id,
                    _record.OutboundNo,
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
    }

    private async Task PrintApprovalAsync()
    {
        try
        {
            var data = await _service.BuildOutboundPrintDataAsync(_record.Id, blankApprovalSignatures: false);
            var document = NetworkOutboundPrintDocumentFactory.Create(data);
            var previewWindow = new PrintPreviewWindow(document)
            {
                Owner = Application.Current.MainWindow
            };
            await _service.RecordOutboundPrintAsync(_record.Id);
            previewWindow.ShowDialog();
            await SyncOutboundPrintMetadataAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("打印生成失败：" + ex.Message);
        }
    }
}
