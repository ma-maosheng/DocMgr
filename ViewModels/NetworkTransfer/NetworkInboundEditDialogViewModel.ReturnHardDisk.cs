using System.Collections.ObjectModel;
using DocMgr.Models;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.HardDiskMedia;
using DocMgr.Services.NetworkTransfer;

namespace DocMgr.ViewModels.NetworkTransfer;

public sealed partial class NetworkInboundEditDialogViewModel
{
    private bool _returnBorrowedHardDiskWithInbound;
    private bool _suppressReturnHardDiskSideEffects;

    /// <summary>档外资料时是否展示借出硬盘归还行（已并入资料介质电子卡片，此处恒为 false）。</summary>
    public bool ShowReturnBorrowedHardDiskSection => false;

    /// <summary>审批环节是否展示借出硬盘归位档口区。</summary>
    public bool ShowReturnHardDiskApprovalSection =>
        ShowApprovalWorkflowPanel
        && ReturnBorrowedHardDiskWithInbound
        && ReturnHardDiskApprovalRows.Count > 0;

    /// <summary>申请环节是否可编辑借出硬盘选择与归还是否。</summary>
    public bool CanEditReturnHardDiskSelection => CanEditHeader && IsExternalSource;

    /// <summary>审批环节是否可指定空白硬盘归位档口。</summary>
    public bool CanEditReturnHardDiskSlots =>
        _mode == NetworkTransferWorkspaceMode.Approval
        && _record.Status is NetworkInboundRecord.StatusSubmitted
            or NetworkInboundRecord.StatusApproved
            or NetworkInboundRecord.StatusSignedUploaded
        && ArchiveRegisterBusinessRules.IsArchiveAdminUser(_userContextService.CurrentUser)
        && ReturnBorrowedHardDiskWithInbound
        && ReturnHardDiskApprovalRows.Count > 0;

    /// <summary>借出硬盘随入网资料归还。</summary>
    public bool ReturnBorrowedHardDiskWithInbound
    {
        get => _returnBorrowedHardDiskWithInbound;
        set
        {
            if (SetProperty(ref _returnBorrowedHardDiskWithInbound, value))
            {
                OnPropertyChanged(nameof(ShowReturnBorrowedHardDiskSection));
                OnPropertyChanged(nameof(ShowReturnHardDiskApprovalSection));
                OnPropertyChanged(nameof(CanEditReturnHardDiskSelection));
                if (!_suppressReturnHardDiskSideEffects && !value)
                {
                    ClearBorrowedHardDiskSelection();
                }
            }
        }
    }

    /// <summary>申请人名下可选择的借出硬盘列表。</summary>
    public ObservableCollection<NetworkInboundBorrowedHardDiskSelectionRowViewModel> BorrowedHardDiskSelectionRows { get; } = new();

    /// <summary>审批环节借出硬盘归位档口行。</summary>
    public ObservableCollection<NetworkInboundReturnHardDiskApprovalRowViewModel> ReturnHardDiskApprovalRows { get; } = new();

    private void BindReturnHardDiskFromRecord()
    {
        _suppressReturnHardDiskSideEffects = true;
        try
        {
            ReturnBorrowedHardDiskWithInbound = _record.ReturnBorrowedHardDiskWithInbound;
        }
        finally
        {
            _suppressReturnHardDiskSideEffects = false;
        }

        OnPropertyChanged(nameof(ShowReturnBorrowedHardDiskSection));
        OnPropertyChanged(nameof(ShowReturnHardDiskApprovalSection));
        OnPropertyChanged(nameof(CanEditReturnHardDiskSlots));
    }

    private async Task InitializeReturnHardDiskSectionsAsync()
    {
        await RebuildReturnHardDiskApprovalRowsAsync(autoSelectRecommended: CanEditReturnHardDiskSlots);
    }

    private async Task LoadBorrowedHardDiskSelectionRowsAsync()
    {
        BorrowedHardDiskSelectionRows.Clear();
        if (!IsExternalSource)
        {
            return;
        }

        try
        {
            User applicant = ResolveApplicantUser();
            IReadOnlyList<HardDiskMediaReturnCandidate> candidates =
                await _hardDiskMediaService.GetBorrowedHardDiskReturnCandidatesForUserAsync(applicant);

            HashSet<string> selectedCodes = (_record.ReturnHardDiskItems ?? [])
                .Select(item => item.DiskCode?.Trim() ?? string.Empty)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (HardDiskMediaReturnCandidate candidate in candidates)
            {
                bool isSelected = selectedCodes.Contains(candidate.DiskCode.Trim());
                BorrowedHardDiskSelectionRows.Add(new NetworkInboundBorrowedHardDiskSelectionRowViewModel(
                    candidate,
                    isSelected,
                    CanEditReturnHardDiskSelection));
            }

            foreach (string savedCode in selectedCodes)
            {
                if (BorrowedHardDiskSelectionRows.Any(row =>
                        string.Equals(row.DiskCode, savedCode, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                BorrowedHardDiskSelectionRows.Insert(0, new NetworkInboundBorrowedHardDiskSelectionRowViewModel(
                    new HardDiskMediaReturnCandidate { DiskCode = savedCode, ApplicantName = ApplicantName, ApplicantDept = ApplicantDept },
                    isSelected: true,
                    isEditable: false));
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError("加载借出硬盘列表失败：" + ex.Message);
        }
    }

    private async Task RebuildReturnHardDiskApprovalRowsAsync(bool autoSelectRecommended)
    {
        ReturnHardDiskApprovalRows.Clear();
        if (!ReturnBorrowedHardDiskWithInbound)
        {
            OnPropertyChanged(nameof(ShowReturnHardDiskApprovalSection));
            return;
        }

        foreach (NetworkInboundReturnHardDiskItem item in (_record.ReturnHardDiskItems ?? [])
                     .OrderBy(row => row.SortOrder)
                     .ThenBy(row => row.Id))
        {
            var row = new NetworkInboundReturnHardDiskApprovalRowViewModel(
                CloneReturnHardDiskItem(item),
                _hardDiskMediaService,
                _cabinetService,
                _dialogService,
                CanEditReturnHardDiskSlots,
                CollectReservedReturnHardDiskSlots);
            ReturnHardDiskApprovalRows.Add(row);
            await row.LoadSlotLocationOptionsAsync(autoSelectRecommended);
        }

        OnPropertyChanged(nameof(ShowReturnHardDiskApprovalSection));
        OnPropertyChanged(nameof(CanEditReturnHardDiskSlots));
    }

    private IReadOnlyCollection<string> CollectReservedReturnHardDiskSlots()
    {
        return ReturnHardDiskApprovalRows
            .Select(row => HardDiskBlankSlotLocationSupport.NormalizeToSlotCode(row.Item.TargetBlankSlotLocation))
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ClearBorrowedHardDiskSelection()
    {
        foreach (NetworkInboundBorrowedHardDiskSelectionRowViewModel row in BorrowedHardDiskSelectionRows)
        {
            row.IsSelected = false;
        }
    }

    private IReadOnlyList<string> GetSelectedReturnHardDiskCodes() =>
        BorrowedHardDiskSelectionRows
            .Where(row => row.IsSelected)
            .Select(row => row.DiskCode.Trim())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private async Task<IReadOnlyList<NetworkInboundReturnHardDiskItem>> BuildReturnHardDiskItemsForSaveAsync()
    {
        if (!IsExternalSource || !ReturnBorrowedHardDiskWithInbound)
        {
            return Array.Empty<NetworkInboundReturnHardDiskItem>();
        }

        User applicant = ResolveApplicantUser();
        IReadOnlyList<HardDiskMediaReturnCandidate> candidates =
            await _hardDiskMediaService.GetBorrowedHardDiskReturnCandidatesForUserAsync(applicant);

        return NetworkInboundReturnHardDiskSupport.BuildReturnHardDiskItems(
            ReturnBorrowedHardDiskWithInbound,
            GetSelectedReturnHardDiskCodes(),
            candidates,
            _record.ReturnHardDiskItems?.ToList());
    }

    private void ClearReturnHardDiskState()
    {
        _suppressReturnHardDiskSideEffects = true;
        try
        {
            ReturnBorrowedHardDiskWithInbound = false;
        }
        finally
        {
            _suppressReturnHardDiskSideEffects = false;
        }

        BorrowedHardDiskSelectionRows.Clear();
        ReturnHardDiskApprovalRows.Clear();
        OnPropertyChanged(nameof(ShowReturnBorrowedHardDiskSection));
        OnPropertyChanged(nameof(ShowReturnHardDiskApprovalSection));
    }

    private async Task ApplyReturnHardDiskToDraftAsync(NetworkInboundRecord draft)
    {
        if (!IsExternalSource)
        {
            draft.ReturnBorrowedHardDiskWithInbound = false;
            draft.ReturnHardDiskItems = [];
            return;
        }

        IReadOnlyList<string> selectedCodes = NetworkInboundReturnHardDiskMediaBridgeSupport.CollectBorrowedHardDiskCodes(
            _electronicMediaEditor.MediaEntries);
        draft.ReturnBorrowedHardDiskWithInbound = selectedCodes.Count > 0;
        if (!draft.ReturnBorrowedHardDiskWithInbound)
        {
            draft.ReturnHardDiskItems = [];
            return;
        }

        User applicant = ResolveApplicantUser();
        IReadOnlyList<HardDiskMediaReturnCandidate> candidates =
            await _hardDiskMediaService.GetBorrowedHardDiskReturnCandidatesForUserAsync(applicant);

        draft.ReturnHardDiskItems = NetworkInboundReturnHardDiskSupport.BuildReturnHardDiskItems(
            returnWithInbound: true,
            selectedCodes,
            candidates,
            _record.ReturnHardDiskItems?.ToList()).ToList();

        foreach (NetworkInboundReturnHardDiskItem saved in _record.ReturnHardDiskItems ?? [])
        {
            NetworkInboundReturnHardDiskItem? built = draft.ReturnHardDiskItems.FirstOrDefault(item =>
                string.Equals(item.DiskCode, saved.DiskCode, StringComparison.OrdinalIgnoreCase));
            if (built != null && !string.IsNullOrWhiteSpace(saved.TargetBlankSlotLocation))
            {
                built.TargetBlankSlotLocation = saved.TargetBlankSlotLocation;
            }
        }

        foreach (NetworkInboundReturnHardDiskApprovalRowViewModel row in ReturnHardDiskApprovalRows)
        {
            NetworkInboundReturnHardDiskItem? built = draft.ReturnHardDiskItems.FirstOrDefault(item =>
                string.Equals(item.DiskCode, row.DiskCode, StringComparison.OrdinalIgnoreCase));
            if (built != null && !string.IsNullOrWhiteSpace(row.Item.TargetBlankSlotLocation))
            {
                built.TargetBlankSlotLocation = row.Item.TargetBlankSlotLocation;
            }
        }
    }

    private async Task PersistReturnHardDiskSlotsIfNeededAsync()
    {
        if (!CanEditReturnHardDiskSlots || ReturnHardDiskApprovalRows.Count == 0)
        {
            return;
        }

        var slotInputs = ReturnHardDiskApprovalRows
            .Select(row => CloneReturnHardDiskItem(row.Item))
            .ToList();
        await _service.UpdateInboundReturnHardDiskSlotsAsync(_record.Id, slotInputs, RequireUser());
    }

    private static NetworkInboundReturnHardDiskItem CloneReturnHardDiskItem(NetworkInboundReturnHardDiskItem item) => new()
    {
        Id = item.Id,
        InboundRecordId = item.InboundRecordId,
        SortOrder = item.SortOrder,
        MediumId = item.MediumId,
        DiskCode = item.DiskCode,
        SourceApplicationId = item.SourceApplicationId,
        SourceOutboundRecordId = item.SourceOutboundRecordId,
        TargetBlankSlotLocation = item.TargetBlankSlotLocation,
        CreatedAt = item.CreatedAt
    };
}
