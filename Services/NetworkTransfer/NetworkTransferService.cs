using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;
using DocMgr.Services.YearlyArchive;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 年度资料出入网管理业务服务。
/// </summary>
public sealed partial class NetworkTransferService : INetworkTransferService
{
    private readonly INetworkTransferRepository _repository;
    private readonly IBusinessRuleService _businessRuleService;
    private readonly IServerPathSettingService _serverPathSettingService;
    private readonly IHardDiskMediaService _hardDiskMediaService;

    public NetworkTransferService(
        INetworkTransferRepository repository,
        IBusinessRuleService businessRuleService,
        IServerPathSettingService serverPathSettingService,
        IHardDiskMediaService hardDiskMediaService)
    {
        _repository = repository;
        _businessRuleService = businessRuleService;
        _serverPathSettingService = serverPathSettingService;
        _hardDiskMediaService = hardDiskMediaService;
    }

    public Task<string> GenerateNextInboundNoAsync() =>
        _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.NetworkInboundApply);

    public Task<string> GenerateNextOutboundNoAsync() =>
        _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.NetworkOutboundApply);

    public Task<string> GenerateNextDisposalNoAsync() =>
        _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.NetworkDisposalApply);

    public async Task<IReadOnlyList<NetworkInboundRecord>> SearchInboundRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear) =>
        await _repository.SearchInboundRecordsAsync(keyword, status, applyYear);

    public Task<NetworkInboundRecord?> GetInboundByIdAsync(int recordId) =>
        _repository.GetInboundByIdAsync(recordId);

    public async Task<IReadOnlyList<NetworkInboundItem>> BuildInboundItemsFromElectronicSearchAsync(
        int resultSetId,
        IReadOnlyCollection<int>? selectedItemIds)
    {
        var resultSet = await _repository.GetElectronicSearchResultSetAsync(resultSetId)
            ?? throw new InvalidOperationException("未找到电子资料检索结果集。已立档入网仅支持电子检索结果。");

        IEnumerable<YearlyArchiveSearchResultSetItem> poolItems = resultSet.Items ?? [];
        if (selectedItemIds != null && selectedItemIds.Count > 0)
        {
            HashSet<int> selected = selectedItemIds.ToHashSet();
            poolItems = poolItems.Where(item => selected.Contains(item.Id));
        }

        List<int> filingFactIds = poolItems
            .Select(item => item.FilingFactId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        Dictionary<int, YearlyArchiveFilingFact> filingFacts =
            await _repository.GetFilingFactsByIdsAsync(filingFactIds);
        EnsureFilingFactsAvailableForInbound(poolItems, filingFacts);

        DateTime now = DateTime.Now;
        int sort = 1;
        var items = new List<NetworkInboundItem>();
        foreach (var poolItem in poolItems.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            filingFacts.TryGetValue(poolItem.FilingFactId, out YearlyArchiveFilingFact? filingFact);
            var inboundItem = new NetworkInboundItem
            {
                SortOrder = sort++,
                AssetKind = NetworkTransferDomainValues.AssetKindJobData,
                AssetName = string.IsNullOrWhiteSpace(poolItem.ItemName)
                    ? poolItem.MaterialName?.Trim() ?? string.Empty
                    : poolItem.ItemName.Trim(),
                ConfidentialLevel = string.Empty,
                DataSizeText = string.Empty,
                TargetServerPath = string.Empty,
                SourceKind = NetworkTransferDomainValues.SourceKindArchivedElectronicSearch,
                SourceResultSetItemId = poolItem.Id,
                SourceFilingFactId = poolItem.FilingFactId,
                FormNo = poolItem.FormNo?.Trim() ?? string.Empty,
                MaterialName = poolItem.MaterialName?.Trim() ?? string.Empty,
                ItemName = poolItem.ItemName?.Trim() ?? string.Empty,
                ContainerCode = poolItem.ContainerCode?.Trim() ?? string.Empty,
                StorageLocation = poolItem.StorageLocation?.Trim() ?? string.Empty,
                CreatedAt = now
            };
            NetworkInboundItemDisplaySupport.ApplyFilingFactSnapshot(inboundItem, filingFact);
            items.Add(inboundItem);
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("检索结果集中没有可导入的明细。");
        }

        return items;
    }

    public async Task<IReadOnlyDictionary<int, YearlyArchiveFilingFact>> GetFilingFactsByIdsAsync(
        IReadOnlyCollection<int> filingFactIds) =>
        await _repository.GetFilingFactsByIdsAsync(filingFactIds);

    public async Task<NetworkInboundRecord> CreateInboundDraftAsync(
        NetworkInboundRecord draft,
        IReadOnlyList<NetworkInboundItem> items,
        User currentUser)
    {
        EnsureApplicant(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        bool isExternalOffline = NetworkTransferDomainValues.IsExternalOfflineSource(draft.SourceKind);
        var builtItems = isExternalOffline
            ? []
            : await NormalizeInboundItemsAsync(draft, items);

        DateTime now = DateTime.Now;
        string inboundNo = string.IsNullOrWhiteSpace(draft.InboundNo)
            ? await GenerateNextInboundNoAsync()
            : draft.InboundNo.Trim();

        var record = new NetworkInboundRecord
        {
            InboundNo = inboundNo,
            Status = NetworkInboundRecord.StatusDraft,
            SourceKind = NetworkTransferDomainValues.NormalizeSourceKind(draft.SourceKind),
            ProvideUnit = NetworkTransferDomainValues.ResolveInboundProvideUnit(draft.SourceKind, draft.ProvideUnit),
            TargetServerPath = ResolveInboundTargetServerPath(draft, builtItems),
            MaterialPath = draft.MaterialPath?.Trim() ?? string.Empty,
            MaterialName = draft.MaterialName?.Trim() ?? string.Empty,
            ProjectName = draft.ProjectName?.Trim() ?? string.Empty,
            Year = draft.Year?.Trim() ?? string.Empty,
            Reason = draft.Reason?.Trim() ?? string.Empty,
            OtherRequests = draft.OtherRequests?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ProofMaterialNote = NormalizeInboundProofMaterialNote(draft.ProofMaterialNote),
            ReturnBorrowedHardDiskWithInbound = draft.ReturnBorrowedHardDiskWithInbound,
            SourceResultSetId = draft.SourceResultSetId,
            SourceResultSetNo = draft.SourceResultSetNo?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = builtItems
        };

        ApplyInboundReturnHardDiskItems(record, draft.ReturnBorrowedHardDiskWithInbound, draft.ReturnHardDiskItems?.ToList());
        record.BusinessChain = NetworkArchiveBusinessChainSupport.CreateForInbound(record, now);

        ValidateInboundHeader(record);
        _repository.AddInbound(record);
        await _repository.SaveChangesAsync();
        if (isExternalOffline)
        {
            await _repository.ReplaceInboundMediaEntriesAsync(
                record.Id,
                draft.MediaEntries?.ToList() ?? []);
            await _repository.SaveChangesAsync();
        }

        NetworkArchiveBusinessChainSupport.SynchronizeInboundTasks(record.BusinessChain, record, now);
        await _repository.SaveChangesAsync();
        return (await _repository.GetInboundByIdAsync(record.Id))!;
    }

    public async Task<NetworkInboundRecord> UpdateInboundDraftAsync(
        NetworkInboundRecord draft,
        IReadOnlyList<NetworkInboundItem> items,
        User currentUser)
    {
        EnsureApplicant(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        var existing = await _repository.GetInboundByIdAsync(draft.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status != NetworkInboundRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态的入网单可编辑。");
        }

        EnsureOwnerOrAdmin(existing.ApplicantUserId, currentUser);
        bool isExternalOffline = NetworkTransferDomainValues.IsExternalOfflineSource(draft.SourceKind);
        var builtItems = isExternalOffline
            ? []
            : await NormalizeInboundItemsAsync(draft, items);

        existing.SourceKind = NetworkTransferDomainValues.NormalizeSourceKind(draft.SourceKind);
        existing.ProvideUnit = NetworkTransferDomainValues.ResolveInboundProvideUnit(draft.SourceKind, draft.ProvideUnit);
        existing.TargetServerPath = ResolveInboundTargetServerPath(draft, builtItems);
        existing.MaterialPath = draft.MaterialPath?.Trim() ?? string.Empty;
        existing.MaterialName = draft.MaterialName?.Trim() ?? string.Empty;
        existing.ProjectName = draft.ProjectName?.Trim() ?? string.Empty;
        existing.Year = draft.Year?.Trim() ?? string.Empty;
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.OtherRequests = draft.OtherRequests?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.ProofMaterialNote = NormalizeInboundProofMaterialNote(draft.ProofMaterialNote);
        existing.ReturnBorrowedHardDiskWithInbound = draft.ReturnBorrowedHardDiskWithInbound;
        existing.SourceResultSetId = draft.SourceResultSetId;
        existing.SourceResultSetNo = draft.SourceResultSetNo?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTime.Now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveInboundItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in builtItems)
        {
            existing.Items.Add(item);
        }

        ApplyInboundReturnHardDiskItems(existing, draft.ReturnBorrowedHardDiskWithInbound, draft.ReturnHardDiskItems?.ToList());
        existing.BusinessChain ??= NetworkArchiveBusinessChainSupport.CreateForInbound(existing, DateTime.Now);
        NetworkArchiveBusinessChainSupport.SynchronizeInboundTasks(existing.BusinessChain, existing, DateTime.Now);

        ValidateInboundHeader(existing);
        await _repository.SaveChangesAsync();
        if (isExternalOffline)
        {
            await _repository.ReplaceInboundMediaEntriesAsync(
                existing.Id,
                draft.MediaEntries?.ToList() ?? []);
            await _repository.SaveChangesAsync();
        }

        return (await _repository.GetInboundByIdAsync(existing.Id))!;
    }

    public async Task SubmitInboundAsync(int recordId, User currentUser)
    {
        EnsureApplicant(currentUser);
        var existing = await _repository.GetInboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status != NetworkInboundRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        EnsureOwnerOrAdmin(existing.ApplicantUserId, currentUser);
        ValidateInboundHeader(existing);
        NetworkInboundApplicationValidationSupport.EnsureValidForSubmit(
            existing,
            existing.Items.ToList(),
            existing.MediaEntries?.ToList());
        await EnsureArchivedInboundFactsAvailableAsync(existing);
        if (NetworkTransferDomainValues.IsExternalOfflineSource(existing.SourceKind))
        {
            if (NetworkInboundExternalMediaValidationSupport.CountMediaItems(existing.MediaEntries) == 0)
            {
                throw new InvalidOperationException("请至少录入一条入网明细。");
            }
        }
        else if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少录入一条入网明细。");
        }

        DateTime now = DateTime.Now;
        existing.BusinessChain ??= NetworkArchiveBusinessChainSupport.CreateForInbound(existing, now);
        NetworkArchiveBusinessChainSupport.SynchronizeInboundTasks(existing.BusinessChain, existing, now);
        existing.Status = NetworkInboundRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        NetworkArchiveBusinessChainSupport.MarkPrimaryInProgress(existing.BusinessChain, now);
        await _repository.SaveChangesAsync();
    }

    public async Task ApproveInboundAsync(NetworkInboundRecord approval, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(approval);
        var existing = await _repository.GetInboundByIdAsync(approval.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status != NetworkInboundRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("仅已提交状态可审批。");
        }

        RequireSigner(approval.DeptLeader, "部门负责人");
        RequireSigner(approval.ProdLeader, "生产管理科负责人");
        RequireSigner(approval.RndLeader, "资料室负责人");
        RequireSigner(approval.DeputyLeader, "分管领导");

        DateTime now = DateTime.Now;
        existing.DeptLeader = approval.DeptLeader.Trim();
        existing.DeptDate = approval.DeptDate ?? now.Date;
        existing.ProdLeader = approval.ProdLeader.Trim();
        existing.ProdDate = approval.ProdDate ?? now.Date;
        existing.RndLeader = approval.RndLeader.Trim();
        existing.RndDate = approval.RndDate ?? now.Date;
        existing.DeputyLeader = approval.DeputyLeader.Trim();
        existing.DeputyDate = approval.DeputyDate ?? now.Date;
        existing.Status = NetworkInboundRecord.StatusApproved;
        existing.ApprovedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task UpdateInboundReturnHardDiskSlotsAsync(
        int recordId,
        IReadOnlyList<NetworkInboundReturnHardDiskItem> slotInputs,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(slotInputs);
        var existing = await _repository.GetInboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (!existing.ReturnBorrowedHardDiskWithInbound || existing.ReturnHardDiskItems.Count == 0)
        {
            return;
        }

        if (existing.Status is not (
            NetworkInboundRecord.StatusSubmitted
            or NetworkInboundRecord.StatusApproved
            or NetworkInboundRecord.StatusSignedUploaded))
        {
            throw new InvalidOperationException("当前状态不允许维护借出硬盘归位档口。");
        }

        NetworkInboundReturnHardDiskSupport.ApplyApprovalSlotLocations(existing, slotInputs);
        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    public async Task ConfirmInboundHandoverAsync(NetworkInboundRecord handover, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(handover);
        var existing = await _repository.GetInboundByIdAsync(handover.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status != NetworkInboundRecord.StatusApproved)
        {
            throw new InvalidOperationException("仅已审批状态可确认交接。");
        }

        NetworkInboundApplicationValidationSupport.EnsureValidForHandoverConfirm(
            existing,
            handover,
            Array.Empty<SystemAttachment>());

        DateTime now = DateTime.Now;
        existing.Deliverer = handover.Deliverer.Trim();
        existing.DeliverDate = handover.DeliverDate ?? now.Date;
        existing.Administrator = handover.Administrator.Trim();
        existing.AdminDate = handover.AdminDate ?? now.Date;
        existing.Status = NetworkInboundRecord.StatusSignedUploaded;
        existing.HandoverConfirmedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task UpdateInboundItemPathsAsync(
        int recordId,
        IReadOnlyList<NetworkInboundItem> items,
        User currentUser,
        string? targetServerPath = null,
        IReadOnlyList<YearlyArchiveRegisterMedia>? externalMediaEntries = null)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetInboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status is not (NetworkInboundRecord.StatusSubmitted or NetworkInboundRecord.StatusApproved))
        {
            throw new InvalidOperationException("仅已提交或已审批状态可补录服务器路径。");
        }

        if (NetworkTransferDomainValues.IsExternalOfflineSource(existing.SourceKind))
        {
            if (!string.IsNullOrWhiteSpace(targetServerPath))
            {
                existing.TargetServerPath = targetServerPath.Trim();
            }

            NetworkInboundApprovalAmendmentSupport.MergeExternalMediaConfidentialLevels(
                existing,
                externalMediaEntries);
            existing.UpdatedAt = DateTime.Now;
            await _repository.SaveChangesAsync();
            return;
        }

        if (items == null)
        {
            throw new InvalidOperationException("明细无效。");
        }

        if (!string.IsNullOrWhiteSpace(targetServerPath))
        {
            existing.TargetServerPath = targetServerPath.Trim();
            foreach (NetworkInboundItem item in existing.Items)
            {
                item.TargetServerPath = existing.TargetServerPath;
            }
        }

        foreach (var item in existing.Items)
        {
            var ui = items.FirstOrDefault(row => row.Id == item.Id)
                ?? items.FirstOrDefault(row =>
                    row.SourceResultSetItemId.HasValue
                    && row.SourceResultSetItemId == item.SourceResultSetItemId);
            if (ui == null)
            {
                continue;
            }

            item.TargetServerPath = ui.TargetServerPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(ui.AssetKind))
            {
                item.AssetKind = ui.AssetKind.Trim();
            }

            if (!string.IsNullOrWhiteSpace(ui.ConfidentialLevel))
            {
                item.ConfidentialLevel = ui.ConfidentialLevel.Trim();
            }

            if (!string.IsNullOrWhiteSpace(ui.DataSizeText))
            {
                item.DataSizeText = ui.DataSizeText.Trim();
            }
        }

        existing.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    public async Task CompleteInboundAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetInboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status == NetworkInboundRecord.StatusCompleted)
        {
            return;
        }

        if (existing.Status != NetworkInboundRecord.StatusSignedUploaded)
        {
            throw new InvalidOperationException("请先确认实物交接并上传签批单后再确认办结。");
        }

        var attachments = await _repository.GetAttachmentsAsync(
            NetworkTransferDomainValues.InboundAttachmentBusinessType,
            existing.InboundNo);
        IReadOnlyList<string> completeErrors = NetworkInboundApplicationValidationSupport.ValidateForComplete(
            existing,
            attachments);
        if (completeErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "附件或审批信息尚未满足办结要求：" + Environment.NewLine + Environment.NewLine
                + string.Join(Environment.NewLine, completeErrors));
        }
        EnsureInboundPathsReady(existing);
        EnsureInboundReturnHardDiskSlotsReady(existing);

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);
        existing.BusinessChain ??= NetworkArchiveBusinessChainSupport.CreateForInbound(existing, now);
        NetworkArchiveBusinessChainSupport.SynchronizeInboundTasks(existing.BusinessChain, existing, now);
        await using IArchiveFilingRepositoryTransaction transaction = await _repository.BeginTransactionAsync();
        try
        {
            if (NetworkTransferDomainValues.IsExternalOfflineSource(existing.SourceKind))
            {
                await CompleteExternalInboundOnNetAssetsAsync(existing, operatorName, now);
            }
            else
            {
                foreach (var item in existing.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id))
                {
                    NetworkOnNetAsset? asset = item.OnNetAssetId.HasValue
                        ? await _repository.GetOnNetAssetByIdAsync(item.OnNetAssetId.Value, tracking: true)
                        : await _repository.GetOnNetAssetByOriginInboundItemIdAsync(item.Id, tracking: true);
                    if (asset == null)
                    {
                        string assetNo = await GenerateNextOnNetAssetNoAsync();
                        asset = new NetworkOnNetAsset
                        {
                            AssetNo = assetNo,
                            AssetKind = item.AssetKind?.Trim() ?? string.Empty,
                            AssetName = item.AssetName?.Trim() ?? string.Empty,
                            ProjectName = existing.ProjectName?.Trim() ?? string.Empty,
                            Year = existing.Year?.Trim() ?? string.Empty,
                            ServerPath = item.TargetServerPath?.Trim() ?? string.Empty,
                            ConfidentialLevel = item.ConfidentialLevel?.Trim() ?? string.Empty,
                            DataSizeText = item.DataSizeText?.Trim() ?? string.Empty,
                            OriginKind = NetworkTransferDomainValues.OriginKindInbound,
                            OriginInboundItemId = item.Id,
                            SourceFilingFactId = item.SourceFilingFactId,
                            LifecycleStatus = NetworkTransferDomainValues.LifecycleOnNet,
                            RegisteredBy = operatorName,
                            RegisteredAt = now,
                            CreatedAt = now,
                            UpdatedAt = now
                        };
                        _repository.AddOnNetAsset(asset);
                    }

                    // 保存以取得资产 Id；处于同一事务中，任一后续步骤失败会整体回滚。
                    await _repository.SaveChangesAsync();
                    item.OnNetAssetId = asset.Id;
                }
            }

            await AddInboundArchiveCopyTransactionsAsync(existing, operatorName, now);
            await CompleteInboundReturnHardDisksAsync(existing, currentUser, now);

            existing.Status = NetworkInboundRecord.StatusCompleted;
            existing.CompletedAt = now;
            existing.CompletedBy = operatorName;
            existing.UpdatedAt = now;
            NetworkArchiveBusinessChainSupport.MarkInboundCompleted(existing.BusinessChain, now);
            await _repository.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task WithdrawInboundAsync(int recordId, string? reason, User currentUser)
    {
        EnsureApplicant(currentUser);
        var existing = await _repository.GetInboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        if (existing.Status is not (NetworkInboundRecord.StatusDraft or NetworkInboundRecord.StatusSubmitted))
        {
            throw new InvalidOperationException("仅草稿或已提交状态可撤回。");
        }

        EnsureOwnerOrAdmin(existing.ApplicantUserId, currentUser);
        DateTime now = DateTime.Now;
        existing.Status = NetworkInboundRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        NetworkArchiveBusinessChainSupport.MarkCancelled(existing.BusinessChain, now);
        await _repository.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<NetworkOutboundRecord>> SearchOutboundRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear) =>
        await _repository.SearchOutboundRecordsAsync(keyword, status, applyYear);

    public Task<NetworkOutboundRecord?> GetOutboundByIdAsync(int recordId) =>
        _repository.GetOutboundByIdAsync(recordId);

    public async Task<IReadOnlyList<NetworkOnNetAsset>> GetSelectableOutboundAssetsAsync(
        int? currentOutboundRecordId = null) =>
        await _repository.GetSelectableOutboundAssetsAsync(currentOutboundRecordId);

    public async Task<NetworkOutboundRecord> CreateOutboundDraftAsync(
        NetworkOutboundRecord draft,
        IReadOnlyList<NetworkOutboundItem> items,
        User currentUser)
    {
        EnsureApplicant(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        var builtItems = NormalizeOutboundItems(draft, items);

        DateTime now = DateTime.Now;
        string outboundNo = string.IsNullOrWhiteSpace(draft.OutboundNo)
            ? await GenerateNextOutboundNoAsync()
            : draft.OutboundNo.Trim();

        var record = new NetworkOutboundRecord
        {
            OutboundNo = outboundNo,
            Status = NetworkOutboundRecord.StatusDraft,
            DestinationKind = draft.DestinationKind?.Trim() ?? string.Empty,
            ProjectName = draft.ProjectName?.Trim() ?? string.Empty,
            Year = draft.Year?.Trim() ?? string.Empty,
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ProofMaterialNote = NormalizeOutboundProofMaterialNote(draft.ProofMaterialNote),
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = builtItems
        };
        record.BusinessChain = NetworkArchiveBusinessChainSupport.CreateForOutbound(record, now);

        ValidateOutboundHeader(record);
        _repository.AddOutbound(record);
        await _repository.SaveChangesAsync();
        NetworkArchiveBusinessChainSupport.SynchronizeOutboundTasks(record.BusinessChain, record, now);
        await _repository.SaveChangesAsync();
        return (await _repository.GetOutboundByIdAsync(record.Id))!;
    }

    public async Task<NetworkOutboundRecord> UpdateOutboundDraftAsync(
        NetworkOutboundRecord draft,
        IReadOnlyList<NetworkOutboundItem> items,
        User currentUser)
    {
        EnsureApplicant(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        var existing = await _repository.GetOutboundByIdAsync(draft.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        if (existing.Status != NetworkOutboundRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态的出网单可编辑。");
        }

        EnsureOwnerOrAdmin(existing.ApplicantUserId, currentUser);
        var builtItems = NormalizeOutboundItems(draft, items);

        existing.DestinationKind = draft.DestinationKind?.Trim() ?? string.Empty;
        existing.ProjectName = draft.ProjectName?.Trim() ?? string.Empty;
        existing.Year = draft.Year?.Trim() ?? string.Empty;
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.ProofMaterialNote = NormalizeOutboundProofMaterialNote(draft.ProofMaterialNote);
        existing.UpdatedAt = DateTime.Now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveOutboundItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in builtItems)
        {
            existing.Items.Add(item);
        }
        existing.BusinessChain ??= NetworkArchiveBusinessChainSupport.CreateForOutbound(existing, DateTime.Now);
        NetworkArchiveBusinessChainSupport.SynchronizeOutboundTasks(existing.BusinessChain, existing, DateTime.Now);

        ValidateOutboundHeader(existing);
        await _repository.SaveChangesAsync();
        return (await _repository.GetOutboundByIdAsync(existing.Id))!;
    }

    public async Task SubmitOutboundAsync(int recordId, User currentUser)
    {
        EnsureApplicant(currentUser);
        var existing = await _repository.GetOutboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        if (existing.Status != NetworkOutboundRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        EnsureOwnerOrAdmin(existing.ApplicantUserId, currentUser);
        ValidateOutboundHeader(existing);
        NetworkOutboundApplicationValidationSupport.EnsureValidForSubmit(existing, existing.Items.ToList());

        await LockOutboundAssetsAsync(existing, NetworkTransferDomainValues.LifecycleOutboundLocked);

        DateTime now = DateTime.Now;
        existing.BusinessChain ??= NetworkArchiveBusinessChainSupport.CreateForOutbound(existing, now);
        NetworkArchiveBusinessChainSupport.SynchronizeOutboundTasks(existing.BusinessChain, existing, now);
        existing.Status = NetworkOutboundRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        NetworkArchiveBusinessChainSupport.MarkPrimaryInProgress(existing.BusinessChain, now);
        await _repository.SaveChangesAsync();
    }

    public async Task ApproveOutboundAsync(NetworkOutboundRecord approval, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(approval);
        var existing = await _repository.GetOutboundByIdAsync(approval.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        if (existing.Status != NetworkOutboundRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("仅已提交状态可审批。");
        }

        RequireSigner(approval.DeptLeader, "部门负责人");
        RequireSigner(approval.ProdLeader, "生产管理科负责人");
        RequireSigner(approval.RndLeader, "资料室负责人");
        RequireSigner(approval.DeputyLeader, "分管领导");

        DateTime now = DateTime.Now;
        existing.DeptLeader = approval.DeptLeader.Trim();
        existing.DeptDate = approval.DeptDate ?? now.Date;
        existing.ProdLeader = approval.ProdLeader.Trim();
        existing.ProdDate = approval.ProdDate ?? now.Date;
        existing.RndLeader = approval.RndLeader.Trim();
        existing.RndDate = approval.RndDate ?? now.Date;
        existing.DeputyLeader = approval.DeputyLeader.Trim();
        existing.DeputyDate = approval.DeputyDate ?? now.Date;
        existing.Status = NetworkOutboundRecord.StatusApproved;
        existing.ApprovedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ConfirmOutboundHandoverAsync(NetworkOutboundRecord handover, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(handover);
        var existing = await _repository.GetOutboundByIdAsync(handover.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        if (existing.Status != NetworkOutboundRecord.StatusApproved)
        {
            throw new InvalidOperationException("仅已审批状态可确认交接。");
        }

        var attachments = await _repository.GetAttachmentsAsync(
            NetworkTransferDomainValues.OutboundAttachmentBusinessType,
            existing.OutboundNo);
        NetworkOutboundApplicationValidationSupport.EnsureValidForHandoverConfirm(existing, handover, attachments);

        DateTime now = DateTime.Now;
        existing.Deliverer = handover.Deliverer.Trim();
        existing.DeliverDate = handover.DeliverDate ?? now.Date;
        existing.Administrator = handover.Administrator.Trim();
        existing.AdminDate = handover.AdminDate ?? now.Date;
        existing.Status = NetworkOutboundRecord.StatusSignedUploaded;
        existing.HandoverConfirmedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task CompleteOutboundAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetOutboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        if (existing.Status == NetworkOutboundRecord.StatusCompleted)
        {
            return;
        }

        if (existing.Status != NetworkOutboundRecord.StatusSignedUploaded)
        {
            throw new InvalidOperationException("请先完成交接确认后再办结。");
        }

        await EnsureSignedAttachmentAsync(
            NetworkTransferDomainValues.OutboundAttachmentBusinessType,
            existing.OutboundNo);

        DateTime now = DateTime.Now;
        string operatorName = ResolveUserDisplayName(currentUser);
        existing.BusinessChain ??= NetworkArchiveBusinessChainSupport.CreateForOutbound(existing, now);
        NetworkArchiveBusinessChainSupport.SynchronizeOutboundTasks(existing.BusinessChain, existing, now);
        await using IArchiveFilingRepositoryTransaction transaction = await _repository.BeginTransactionAsync();
        try
        {
            YearlyArchiveRegisterRecord? register = null;
            if (NetworkTransferDomainValues.IsArchiveFilingDestination(existing.DestinationKind))
            {
                register = await _repository.GetRegisterBySourceOutboundRecordIdAsync(existing.Id, tracking: true);
                if (register == null)
                {
                    register = CreateArchiveRegisterDraftFromOutbound(existing, currentUser, now);
                    register.FormNo = await _businessRuleService.GenerateBusinessNoAsync(BusinessNoCategory.AssetInboundApply);
                    _repository.AddRegisterRecord(register);
                    await _repository.SaveChangesAsync();
                }

                existing.TargetRegisterRecordId = register.Id;
                existing.TargetRegisterFormNo = register.FormNo;
            }

            var assets = await _repository.GetOnNetAssetsByIdsAsync(
                GetLinkedOnNetAssetIds(existing.Items).ToList(),
                tracking: true);
            foreach (var asset in assets)
            {
                asset.LifecycleStatus = NetworkTransferDomainValues.LifecycleOutbounded;
                asset.UpdatedAt = now;
            }

            existing.Status = NetworkOutboundRecord.StatusCompleted;
            existing.CompletedAt = now;
            existing.CompletedBy = operatorName;
            existing.UpdatedAt = now;
            NetworkArchiveBusinessChainSupport.MarkOutboundCompleted(existing.BusinessChain, register, now);
            await _repository.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task WithdrawOutboundAsync(int recordId, string? reason, User currentUser)
    {
        EnsureApplicant(currentUser);
        var existing = await _repository.GetOutboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        if (existing.Status is not (NetworkOutboundRecord.StatusDraft or NetworkOutboundRecord.StatusSubmitted))
        {
            throw new InvalidOperationException("仅草稿或已提交状态可撤回。");
        }

        EnsureOwnerOrAdmin(existing.ApplicantUserId, currentUser);

        if (existing.Status == NetworkOutboundRecord.StatusSubmitted)
        {
            await UnlockAssetsAsync(
                GetLinkedOnNetAssetIds(existing.Items).ToList(),
                NetworkTransferDomainValues.LifecycleOutboundLocked,
                NetworkTransferDomainValues.LifecycleOnNet);
        }

        DateTime now = DateTime.Now;
        existing.Status = NetworkOutboundRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        NetworkArchiveBusinessChainSupport.MarkCancelled(existing.BusinessChain, now);
        await _repository.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<NetworkOnNetAsset>> SearchOnNetAssetsAsync(
        string? keyword,
        string? originKind,
        string? lifecycleStatus) =>
        await _repository.SearchOnNetAssetsAsync(keyword, originKind, lifecycleStatus);

    public async Task<NetworkOnNetAsset> RegisterProcessedOutputAsync(NetworkOnNetAsset draft, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);

        if (string.IsNullOrWhiteSpace(draft.AssetName))
        {
            throw new InvalidOperationException("请填写加工产出名称。");
        }

        if (string.IsNullOrWhiteSpace(draft.ServerPath))
        {
            throw new InvalidOperationException("请填写服务器路径。");
        }

        string assetKind = draft.AssetKind?.Trim() ?? string.Empty;
        if (!NetworkTransferDomainValues.AssetKindOptions.Contains(assetKind, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("请选择有效的资料类别。");
        }

        if (draft.ParentAssetId.HasValue && draft.ParentAssetId.Value > 0)
        {
            var parent = await _repository.GetOnNetAssetByIdAsync(draft.ParentAssetId.Value)
                ?? throw new InvalidOperationException("未找到父级在网对象。");
            if (!string.Equals(parent.LifecycleStatus, NetworkTransferDomainValues.LifecycleOnNet, StringComparison.Ordinal)
                && !string.Equals(parent.LifecycleStatus, NetworkTransferDomainValues.LifecycleOutbounded, StringComparison.Ordinal)
                && !string.Equals(parent.LifecycleStatus, NetworkTransferDomainValues.LifecycleDisposed, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("父级在网对象状态异常，无法建立血缘。");
            }
        }

        DateTime now = DateTime.Now;
        var asset = new NetworkOnNetAsset
        {
            AssetNo = await GenerateNextOnNetAssetNoAsync(),
            AssetKind = assetKind,
            AssetName = draft.AssetName.Trim(),
            ProjectName = draft.ProjectName?.Trim() ?? string.Empty,
            Year = draft.Year?.Trim() ?? string.Empty,
            ServerPath = draft.ServerPath.Trim(),
            ConfidentialLevel = draft.ConfidentialLevel?.Trim() ?? string.Empty,
            DataSizeText = draft.DataSizeText?.Trim() ?? string.Empty,
            VersionText = draft.VersionText?.Trim() ?? string.Empty,
            OriginKind = NetworkTransferDomainValues.OriginKindProcessedOutput,
            ParentAssetId = draft.ParentAssetId is > 0 ? draft.ParentAssetId : null,
            LifecycleStatus = NetworkTransferDomainValues.LifecycleOnNet,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            RegisteredBy = ResolveUserDisplayName(currentUser),
            RegisteredAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _repository.AddOnNetAsset(asset);
        await _repository.SaveChangesAsync();
        return (await _repository.GetOnNetAssetByIdAsync(asset.Id))!;
    }

    public async Task<IReadOnlyList<NetworkOnNetDisposalRecord>> SearchDisposalRecordsAsync(
        string? keyword,
        int? status,
        int? applyYear) =>
        await _repository.SearchDisposalRecordsAsync(keyword, status, applyYear);

    public Task<NetworkOnNetDisposalRecord?> GetDisposalByIdAsync(int recordId) =>
        _repository.GetDisposalByIdAsync(recordId);

    public async Task<IReadOnlyList<NetworkOnNetAsset>> GetSelectableDisposalAssetsAsync(
        int? currentDisposalRecordId = null) =>
        await _repository.GetSelectableDisposalAssetsAsync(currentDisposalRecordId);

    public async Task<NetworkOnNetDisposalRecord> CreateDisposalDraftAsync(
        NetworkOnNetDisposalRecord draft,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        var builtItems = await BuildDisposalItemsAsync(items, currentDisposalRecordId: null);

        DateTime now = DateTime.Now;
        string disposalNo = string.IsNullOrWhiteSpace(draft.DisposalNo)
            ? await GenerateNextDisposalNoAsync()
            : draft.DisposalNo.Trim();

        var record = new NetworkOnNetDisposalRecord
        {
            DisposalNo = disposalNo,
            Status = NetworkOnNetDisposalRecord.StatusDraft,
            DisposalReason = BuildDistinctSummary(builtItems.Select(item => item.DisposalReason)),
            DispositionMethod = BuildDistinctSummary(builtItems.Select(item => item.DispositionMethod)),
            Reason = draft.Reason?.Trim() ?? string.Empty,
            Remark = draft.Remark?.Trim() ?? string.Empty,
            ApplicantUserId = currentUser.Id,
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            ApplyTime = now,
            CreatedAt = now,
            UpdatedAt = now,
            Items = builtItems
        };

        if (record.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条在网对象。");
        }

        _repository.AddDisposal(record);
        await _repository.SaveChangesAsync();
        return (await _repository.GetDisposalByIdAsync(record.Id))!;
    }

    public async Task<NetworkOnNetDisposalRecord> UpdateDisposalDraftAsync(
        NetworkOnNetDisposalRecord draft,
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        ArgumentNullException.ThrowIfNull(draft);
        var existing = await _repository.GetDisposalByIdAsync(draft.Id, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (existing.Status != NetworkOnNetDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态的处置单可编辑。");
        }

        var builtItems = await BuildDisposalItemsAsync(items, existing.Id);
        existing.DisposalReason = BuildDistinctSummary(builtItems.Select(item => item.DisposalReason));
        existing.DispositionMethod = BuildDistinctSummary(builtItems.Select(item => item.DispositionMethod));
        existing.Reason = draft.Reason?.Trim() ?? string.Empty;
        existing.Remark = draft.Remark?.Trim() ?? string.Empty;
        existing.UpdatedAt = DateTime.Now;

        if (existing.Items.Count > 0)
        {
            _repository.RemoveDisposalItems(existing.Items.ToList());
            existing.Items.Clear();
        }

        foreach (var item in builtItems)
        {
            existing.Items.Add(item);
        }

        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条在网对象。");
        }

        await _repository.SaveChangesAsync();
        return (await _repository.GetDisposalByIdAsync(existing.Id))!;
    }

    public async Task SubmitDisposalAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetDisposalByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (existing.Status != NetworkOnNetDisposalRecord.StatusDraft)
        {
            throw new InvalidOperationException("仅草稿状态可提交。");
        }

        if (existing.Items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条在网对象。");
        }

        await LockDisposalAssetsAsync(existing);

        DateTime now = DateTime.Now;
        existing.Status = NetworkOnNetDisposalRecord.StatusSubmitted;
        existing.SubmittedAt = now;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ApproveDisposalAsync(int recordId, string approvalOpinion, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetDisposalByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (existing.Status != NetworkOnNetDisposalRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("仅已提交状态可审批。");
        }

        DateTime now = DateTime.Now;
        existing.ApprovedBy = ResolveUserDisplayName(currentUser);
        existing.ApprovedTime = now;
        existing.ApprovalOpinion = approvalOpinion?.Trim() ?? string.Empty;
        existing.Status = NetworkOnNetDisposalRecord.StatusApproved;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task ConfirmDisposalReadyForUploadAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetDisposalByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (existing.Status != NetworkOnNetDisposalRecord.StatusApproved)
        {
            throw new InvalidOperationException("仅已审批状态可确认可上传。");
        }

        DateTime now = DateTime.Now;
        existing.ConfirmedBy = ResolveUserDisplayName(currentUser);
        existing.ConfirmedTime = now;
        existing.Status = NetworkOnNetDisposalRecord.StatusSignedUploaded;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task CompleteDisposalAsync(int recordId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetDisposalByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (existing.Status != NetworkOnNetDisposalRecord.StatusSignedUploaded)
        {
            throw new InvalidOperationException("请先确认可上传并上传签批单后再办结。");
        }

        await EnsureSignedAttachmentAsync(
            NetworkTransferDomainValues.DisposalAttachmentBusinessType,
            existing.DisposalNo);

        DateTime now = DateTime.Now;
        var assets = await _repository.GetOnNetAssetsByIdsAsync(
            existing.Items.Select(item => item.OnNetAssetId).ToList(),
            tracking: true);
        foreach (var asset in assets)
        {
            asset.LifecycleStatus = NetworkTransferDomainValues.LifecycleDisposed;
            asset.UpdatedAt = now;
        }

        existing.Status = NetworkOnNetDisposalRecord.StatusCompleted;
        existing.CompletedAt = now;
        existing.CompletedBy = ResolveUserDisplayName(currentUser);
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task WithdrawDisposalAsync(int recordId, string? reason, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var existing = await _repository.GetDisposalByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (existing.Status is not (NetworkOnNetDisposalRecord.StatusDraft or NetworkOnNetDisposalRecord.StatusSubmitted))
        {
            throw new InvalidOperationException("仅草稿或已提交状态可撤回。");
        }

        if (existing.Status == NetworkOnNetDisposalRecord.StatusSubmitted)
        {
            await UnlockAssetsAsync(
                existing.Items.Select(item => item.OnNetAssetId).ToList(),
                NetworkTransferDomainValues.LifecycleDisposalLocked,
                NetworkTransferDomainValues.LifecycleOnNet);
        }

        DateTime now = DateTime.Now;
        existing.Status = NetworkOnNetDisposalRecord.StatusWithdrawn;
        existing.WithdrawnAt = now;
        existing.WithdrawReason = reason?.Trim() ?? string.Empty;
        existing.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<SystemAttachment>> GetAttachmentsAsync(string businessType, string businessNo) =>
        await _repository.GetAttachmentsAsync(businessType, businessNo);

    public async Task<(bool Ok, string Message, SystemAttachment? Attachment)> UploadAttachmentAsync(
        string businessType,
        int recordId,
        string businessNo,
        string fileCategory,
        string fileName,
        string extension,
        long fileSize,
        byte[] fileContent,
        User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        if (string.IsNullOrWhiteSpace(fileCategory))
        {
            return (false, "请选择附件分类。", null);
        }

        if (fileContent == null || fileContent.Length == 0)
        {
            return (false, "附件内容为空。", null);
        }

        var attachment = new SystemAttachment
        {
            BusinessType = businessType.Trim(),
            BusinessNo = businessNo.Trim(),
            BusinessId = recordId,
            FileName = fileName?.Trim() ?? string.Empty,
            Extension = extension?.Trim() ?? string.Empty,
            FileSize = fileSize,
            FileContent = fileContent,
            FileCategory = fileCategory.Trim(),
            UploadTime = DateTime.Now,
            UploaderName = ResolveUserDisplayName(currentUser)
        };

        _repository.AddAttachment(attachment);

        if (string.Equals(
                fileCategory.Trim(),
                NetworkTransferDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal))
        {
            await MarkSignedUploadedAsync(businessType, recordId, currentUser);
        }

        await _repository.SaveChangesAsync();
        return (true, "上传成功。", attachment);
    }

    public async Task<(bool Ok, string Message)> DeleteAttachmentAsync(int attachmentId, User currentUser)
    {
        EnsureArchiveAdmin(currentUser);
        var attachment = await _repository.GetAttachmentByIdAsync(attachmentId);
        if (attachment == null)
        {
            return (false, "附件不存在。");
        }

        _repository.RemoveAttachment(attachment);
        await _repository.SaveChangesAsync();
        return (true, "已删除。");
    }

    private async Task MarkSignedUploadedAsync(string businessType, int recordId, User currentUser)
    {
        DateTime now = DateTime.Now;
        string uploader = ResolveUserDisplayName(currentUser);
        if (string.Equals(businessType, NetworkTransferDomainValues.InboundAttachmentBusinessType, StringComparison.Ordinal))
        {
            var record = await _repository.GetInboundByIdAsync(recordId, tracking: true);
            if (record != null)
            {
                record.SignedAttachmentUploaded = true;
                record.SignedAttachmentUploadedTime = now;
                record.SignedAttachmentUploader = uploader;
                record.UpdatedAt = now;
            }

            return;
        }

        if (string.Equals(businessType, NetworkTransferDomainValues.OutboundAttachmentBusinessType, StringComparison.Ordinal))
        {
            var record = await _repository.GetOutboundByIdAsync(recordId, tracking: true);
            if (record != null)
            {
                record.SignedAttachmentUploaded = true;
                record.SignedAttachmentUploadedTime = now;
                record.SignedAttachmentUploader = uploader;
                record.UpdatedAt = now;
            }

            return;
        }

        if (string.Equals(businessType, NetworkTransferDomainValues.DisposalAttachmentBusinessType, StringComparison.Ordinal))
        {
            var record = await _repository.GetDisposalByIdAsync(recordId, tracking: true);
            if (record != null)
            {
                record.SignedAttachmentUploaded = true;
                record.SignedAttachmentUploadedTime = now;
                record.SignedAttachmentUploader = uploader;
                record.UpdatedAt = now;
            }
        }
    }

    private async Task<List<NetworkInboundItem>> NormalizeInboundItemsAsync(
        NetworkInboundRecord draft,
        IReadOnlyList<NetworkInboundItem> items)
    {
        string sourceKind = draft.SourceKind?.Trim() ?? string.Empty;
        if (!NetworkTransferDomainValues.IsValidSourceKind(sourceKind))
        {
            throw new InvalidOperationException("请选择有效的数据源类别。");
        }

        if (NetworkTransferDomainValues.IsExternalOfflineSource(sourceKind))
        {
            return [];
        }

        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(sourceKind))
        {
            if (!draft.SourceResultSetId.HasValue || draft.SourceResultSetId.Value <= 0)
            {
                throw new InvalidOperationException("立档资料入网必须挂接电子资料检索结果集。");
            }

            var selectedIds = items?
                .Where(item => item.SourceResultSetItemId.HasValue)
                .Select(item => item.SourceResultSetItemId!.Value)
                .ToList();

            var imported = await BuildInboundItemsFromElectronicSearchAsync(
                draft.SourceResultSetId.Value,
                selectedIds is { Count: > 0 } ? selectedIds : null);

            // 保留用户填写的服务器路径/密级/体量/类别
            var pathMap = (items ?? Array.Empty<NetworkInboundItem>())
                .Where(item => item.SourceResultSetItemId.HasValue)
                .ToDictionary(item => item.SourceResultSetItemId!.Value, item => item);

            foreach (var item in imported)
            {
                if (item.SourceResultSetItemId.HasValue
                    && pathMap.TryGetValue(item.SourceResultSetItemId.Value, out var edited))
                {
                    if (!string.IsNullOrWhiteSpace(edited.AssetKind))
                    {
                        item.AssetKind = edited.AssetKind.Trim();
                    }

                    item.ConfidentialLevel = edited.ConfidentialLevel?.Trim() ?? string.Empty;
                    item.DataSizeText = edited.DataSizeText?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(edited.AssetName))
                    {
                        item.AssetName = edited.AssetName.Trim();
                    }
                }
            }

            string sharedPath = (items ?? Array.Empty<NetworkInboundItem>())
                .Select(row => row.TargetServerPath?.Trim())
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? pathMap.Values
                    .Select(row => row.TargetServerPath?.Trim())
                    .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(sharedPath))
            {
                foreach (NetworkInboundItem item in imported)
                {
                    item.TargetServerPath = sharedPath;
                }
            }

            return imported.ToList();
        }

        if (items == null || items.Count == 0)
        {
            throw new InvalidOperationException("请至少录入一条入网明细。");
        }

        DateTime now = DateTime.Now;
        int sort = 1;
        var result = new List<NetworkInboundItem>();
        foreach (var item in items)
        {
            string assetKind = item.AssetKind?.Trim() ?? string.Empty;
            string dataSizeText = item.DataSizeText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dataSizeText)
                && NetworkInboundItemDisplaySupport.TryParseDataSizeText(dataSizeText, out decimal parsed, out string unit))
            {
                dataSizeText = NetworkInboundItemDisplaySupport.ComposeDataSizeText(parsed, unit);
            }

            result.Add(new NetworkInboundItem
            {
                SortOrder = sort++,
                AssetKind = assetKind,
                AssetName = item.AssetName?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim() ?? string.Empty,
                ConfidentialLevel = item.ConfidentialLevel?.Trim() ?? string.Empty,
                DataSizeText = dataSizeText,
                TargetServerPath = item.TargetServerPath?.Trim() ?? string.Empty,
                SourceKind = sourceKind,
                CreatedAt = now
            });
        }

        return result;
    }

    private static List<NetworkOutboundItem> NormalizeOutboundItems(
        NetworkOutboundRecord draft,
        IReadOnlyList<NetworkOutboundItem> items)
    {
        if (items == null || items.Count == 0)
        {
            throw new InvalidOperationException("请至少录入一条出网明细。");
        }

        string sharedPath = items
            .Select(item => item.ServerPath?.Trim())
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
            ?? string.Empty;

        DateTime now = DateTime.Now;
        int sort = 1;
        var result = new List<NetworkOutboundItem>();
        foreach (var item in items)
        {
            string assetKind = item.AssetKind?.Trim() ?? string.Empty;
            string dataSizeText = item.DataSizeText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dataSizeText)
                && NetworkInboundItemDisplaySupport.TryParseDataSizeText(dataSizeText, out decimal parsed, out string unit))
            {
                dataSizeText = NetworkInboundItemDisplaySupport.ComposeDataSizeText(parsed, unit);
            }

            result.Add(new NetworkOutboundItem
            {
                SortOrder = sort++,
                OnNetAssetId = item.OnNetAssetId is > 0 ? item.OnNetAssetId : null,
                AssetNo = item.AssetNo?.Trim() ?? string.Empty,
                AssetKind = assetKind,
                AssetName = item.AssetName?.Trim() ?? string.Empty,
                ItemName = item.ItemName?.Trim() ?? string.Empty,
                ServerPath = !string.IsNullOrWhiteSpace(sharedPath)
                    ? sharedPath
                    : item.ServerPath?.Trim() ?? string.Empty,
                ConfidentialLevel = item.ConfidentialLevel?.Trim() ?? string.Empty,
                DataSizeText = dataSizeText,
                ProjectName = draft.ProjectName?.Trim() ?? string.Empty,
                Year = draft.Year?.Trim() ?? string.Empty,
                CreatedAt = now
            });
        }

        return result;
    }

    private static IEnumerable<int> GetLinkedOnNetAssetIds(IEnumerable<NetworkOutboundItem> items) =>
        items.Where(item => item.OnNetAssetId is > 0).Select(item => item.OnNetAssetId!.Value);

    private async Task<List<NetworkOnNetDisposalItem>> BuildDisposalItemsAsync(
        IReadOnlyList<NetworkOnNetDisposalItem> items,
        int? currentDisposalRecordId)
    {
        if (items == null || items.Count == 0)
        {
            throw new InvalidOperationException("请至少选择一条在网对象。");
        }

        var selectable = await _repository.GetSelectableDisposalAssetsAsync(currentDisposalRecordId);
        var selectableMap = selectable.ToDictionary(item => item.Id);
        DateTime now = DateTime.Now;
        int sort = 1;
        var result = new List<NetworkOnNetDisposalItem>();
        foreach (var item in items)
        {
            if (!selectableMap.TryGetValue(item.OnNetAssetId, out var asset))
            {
                throw new InvalidOperationException($"在网对象不可处置：Id={item.OnNetAssetId}。");
            }

            string reason = item.DisposalReason?.Trim() ?? string.Empty;
            string method = item.DispositionMethod?.Trim() ?? string.Empty;
            if (!NetworkTransferDomainValues.DisposalReasonOptions.Contains(reason, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("请选择有效的处置原因。");
            }

            if (!NetworkTransferDomainValues.DisposalMethodOptions.Contains(method, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("请选择有效的处置方式。");
            }

            result.Add(new NetworkOnNetDisposalItem
            {
                SortOrder = sort++,
                OnNetAssetId = asset.Id,
                AssetNo = asset.AssetNo,
                AssetKind = asset.AssetKind,
                AssetName = asset.AssetName,
                ServerPath = asset.ServerPath,
                BeforeLifecycleStatus = asset.LifecycleStatus,
                DisposalReason = reason,
                DispositionMethod = method,
                CreatedAt = now
            });
        }

        return result;
    }

    private async Task LockOutboundAssetsAsync(NetworkOutboundRecord record, string lockedStatus)
    {
        var assetIds = GetLinkedOnNetAssetIds(record.Items).ToList();
        if (assetIds.Count == 0)
        {
            return;
        }

        var assets = await _repository.GetOnNetAssetsByIdsAsync(assetIds, tracking: true);
        DateTime now = DateTime.Now;
        foreach (var asset in assets)
        {
            if (!NetworkTransferDomainValues.CanOutbound(asset.OriginKind, asset.LifecycleStatus))
            {
                throw new InvalidOperationException($"对象「{asset.AssetNo}」当前不可出网。");
            }

            asset.LifecycleStatus = lockedStatus;
            asset.UpdatedAt = now;
        }
    }

    private async Task LockDisposalAssetsAsync(NetworkOnNetDisposalRecord record)
    {
        var assets = await _repository.GetOnNetAssetsByIdsAsync(
            record.Items.Select(item => item.OnNetAssetId).ToList(),
            tracking: true);
        DateTime now = DateTime.Now;
        foreach (var asset in assets)
        {
            if (!NetworkTransferDomainValues.CanDispose(asset.LifecycleStatus))
            {
                throw new InvalidOperationException($"对象「{asset.AssetNo}」当前不可处置。");
            }

            asset.LifecycleStatus = NetworkTransferDomainValues.LifecycleDisposalLocked;
            asset.UpdatedAt = now;
        }
    }

    private async Task UnlockAssetsAsync(
        IReadOnlyList<int> assetIds,
        string expectedStatus,
        string restoreStatus)
    {
        var assets = await _repository.GetOnNetAssetsByIdsAsync(assetIds, tracking: true);
        DateTime now = DateTime.Now;
        foreach (var asset in assets)
        {
            if (string.Equals(asset.LifecycleStatus, expectedStatus, StringComparison.Ordinal))
            {
                asset.LifecycleStatus = restoreStatus;
                asset.UpdatedAt = now;
            }
        }
    }

    private YearlyArchiveRegisterRecord CreateArchiveRegisterDraftFromOutbound(
        NetworkOutboundRecord outbound,
        User currentUser,
        DateTime now)
    {
        string materialName = outbound.Items
            .OrderBy(item => item.SortOrder)
            .Select(item => item.AssetName)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? $"出网转入-{outbound.OutboundNo}";

        var register = new YearlyArchiveRegisterRecord
        {
            FormNo = string.Empty,
            Status = YearlyArchiveRegisterRecord.Draft,
            CreatedDate = now,
            ApplicantDate = now,
            ProjectName = string.IsNullOrWhiteSpace(outbound.ProjectName) ? null : outbound.ProjectName.Trim(),
            MaterialName = materialName.Trim(),
            SourceType = NetworkTransferDomainValues.RegisterSourceTypeNetworkOutbound,
            ProvideUnit = outbound.ApplicantDept?.Trim() ?? string.Empty,
            ProofMaterialNote = NormalizeOutboundProofMaterialNote(outbound.ProofMaterialNote),
            ApplicantName = ResolveUserDisplayName(currentUser),
            ApplicantDept = currentUser.Department?.Trim() ?? string.Empty,
            OtherRequests = $"由出网单 {outbound.OutboundNo} 办结自动生成草稿；资料明细已带入，请确认介质类型、所属子类及归档目的。",
            SourceNetworkOutboundRecordId = outbound.Id,
            SourceNetworkOutboundNo = outbound.OutboundNo,
            BusinessChainId = outbound.BusinessChainId
        };

        var media = new YearlyArchiveRegisterMedia
        {
            MediaKind = ArchiveRegisterDomainValues.MediaKindElectronic,
            MediaType = ArchiveRegisterDomainValues.ElectronicMediaTypeInnerNetwork,
            MediaCount = 1,
            Disposition = ArchiveRegisterDomainValues.ElectronicDispositionNone
        };

        foreach (NetworkOutboundItem outboundItem in outbound.Items
                     .OrderBy(item => item.SortOrder)
                     .ThenBy(item => item.Id))
        {
            string contentDescription = string.IsNullOrWhiteSpace(outboundItem.ItemName)
                ? outboundItem.AssetName?.Trim() ?? string.Empty
                : outboundItem.ItemName.Trim();
            var mediaItem = new YearlyArchiveRegisterMediaItem
            {
                ItemType = ArchiveRegisterDomainValues.ItemTypeData,
                ContentDesc = contentDescription,
                ContentCount = 1,
                StoragePath = outboundItem.ServerPath?.Trim() ?? string.Empty,
                Note = string.IsNullOrWhiteSpace(outboundItem.AssetNo)
                    ? $"来源出网单 {outbound.OutboundNo}"
                    : $"来源在网资产 {outboundItem.AssetNo.Trim()}；出网单 {outbound.OutboundNo}",
                ConfidentialLevel = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(
                    outboundItem.ConfidentialLevel),
                ElectronicDetail = new YearlyArchiveRegisterElectronicMediaItemDetail
                {
                    MaterialCategory = ArchiveRegisterDomainValues.ElectronicMaterialCategoryData,
                    SubCategory = string.Empty,
                    DataOrganizationForm = ArchiveRegisterDomainValues.ElectronicDataOrganizationFormDirectory,
                    DataSizeMb = ConvertDataSizeTextToMb(outboundItem.DataSizeText)
                }
            };
            media.Items.Add(mediaItem);
        }

        register.MediaEntries.Add(media);
        return register;
    }

    private async Task AddInboundArchiveCopyTransactionsAsync(
        NetworkInboundRecord record,
        string operatorName,
        DateTime operatedAt)
    {
        if (!NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind))
        {
            return;
        }

        List<NetworkInboundItem> linkedItems = record.Items
            .Where(item => item.SourceFilingFactId is > 0)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToList();
        Dictionary<int, YearlyArchiveFilingFact> facts = await _repository.GetFilingFactsByIdsAsync(
            linkedItems.Select(item => item.SourceFilingFactId!.Value).Distinct().ToList());

        List<int> missingFactIds = linkedItems
            .Select(item => item.SourceFilingFactId!.Value)
            .Distinct()
            .Where(id => !facts.ContainsKey(id))
            .ToList();
        if (missingFactIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"有 {missingFactIds.Count} 条来源立档事实已不存在，无法办结复制入网。");
        }

        foreach (YearlyArchiveFilingFact fact in facts.Values)
        {
            if (!string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"立档资料「{fact.MaterialName}/{fact.ItemName}」当前状态为"
                    + $"“{MaterialTransactionDomainValues.MapLifecycleStatusDisplay(fact.LifecycleStatus)}”，不可办结复制入网。");
            }
        }

        List<YearlyArchiveMaterialTransaction> candidates = linkedItems
            .Where(item => facts.ContainsKey(item.SourceFilingFactId!.Value))
            .Select(item => ArchiveMaterialTransactionSupport.BuildNetworkInboundCopyTransaction(
                record,
                item,
                facts[item.SourceFilingFactId!.Value],
                operatorName,
                operatedAt))
            .ToList();
        HashSet<string> existingKeys = await _repository.GetExistingMaterialTransactionDedupKeysAsync(
            candidates.Select(item => item.DedupKey));
        _repository.AddMaterialTransactions(
            candidates.Where(item => !existingKeys.Contains(item.DedupKey)));
    }

    private async Task EnsureArchivedInboundFactsAvailableAsync(NetworkInboundRecord record)
    {
        if (!NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind))
        {
            return;
        }

        List<int> filingFactIds = record.Items
            .Where(item => item.SourceFilingFactId is > 0)
            .Select(item => item.SourceFilingFactId!.Value)
            .Distinct()
            .ToList();
        Dictionary<int, YearlyArchiveFilingFact> facts =
            await _repository.GetFilingFactsByIdsAsync(filingFactIds);
        var unavailable = new List<string>();
        foreach (NetworkInboundItem item in record.Items.OrderBy(row => row.SortOrder))
        {
            if (!item.SourceFilingFactId.HasValue
                || !facts.TryGetValue(item.SourceFilingFactId.Value, out YearlyArchiveFilingFact? fact))
            {
                unavailable.Add($"{item.AssetName}（立档事实不存在）");
                continue;
            }

            if (!string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal))
            {
                unavailable.Add(
                    $"{fact.MaterialName}/{fact.ItemName}（"
                    + $"{MaterialTransactionDomainValues.MapLifecycleStatusDisplay(fact.LifecycleStatus)}）");
            }
        }

        if (unavailable.Count > 0)
        {
            throw new InvalidOperationException(
                "以下立档资料当前不可复制入网：" + Environment.NewLine
                + string.Join(Environment.NewLine, unavailable.Select(item => $"• {item}")));
        }
    }

    private static decimal ConvertDataSizeTextToMb(string? dataSizeText)
    {
        if (!NetworkInboundItemDisplaySupport.TryParseDataSizeText(dataSizeText, out decimal value, out string unit))
        {
            return 0m;
        }

        return unit switch
        {
            "GB" => value * 1024m,
            "TB" => value * 1024m * 1024m,
            _ => value
        };
    }

    private static void EnsureFilingFactsAvailableForInbound(
        IEnumerable<YearlyArchiveSearchResultSetItem> poolItems,
        IReadOnlyDictionary<int, YearlyArchiveFilingFact> filingFacts)
    {
        var unavailable = new List<string>();
        foreach (YearlyArchiveSearchResultSetItem poolItem in poolItems)
        {
            if (!filingFacts.TryGetValue(poolItem.FilingFactId, out YearlyArchiveFilingFact? fact))
            {
                unavailable.Add($"{poolItem.MaterialName}/{poolItem.ItemName}（立档事实不存在）");
                continue;
            }

            if (!string.Equals(fact.LifecycleStatus, FilingFactLifecycleStatus.InArchive, StringComparison.Ordinal))
            {
                unavailable.Add(
                    $"{fact.MaterialName}/{fact.ItemName}（当前状态："
                    + $"{MaterialTransactionDomainValues.MapLifecycleStatusDisplay(fact.LifecycleStatus)}）");
            }
        }

        if (unavailable.Count > 0)
        {
            throw new InvalidOperationException(
                "以下立档资料当前不可复制入网：" + Environment.NewLine
                + string.Join(Environment.NewLine, unavailable.Select(item => $"• {item}")));
        }
    }

    private async Task EnsureSignedAttachmentAsync(string businessType, string businessNo)
    {
        var attachments = await _repository.GetAttachmentsAsync(businessType, businessNo);
        bool hasSigned = attachments.Any(item =>
            string.Equals(
                item.FileCategory?.Trim(),
                NetworkTransferDomainValues.AttachmentCategorySignedForm,
                StringComparison.Ordinal));
        if (!hasSigned)
        {
            throw new InvalidOperationException("请先上传签批单后再办结。");
        }
    }

    private static void ValidateInboundHeader(NetworkInboundRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.SourceKind))
        {
            throw new InvalidOperationException("请选择数据源类别。");
        }

        if (string.IsNullOrWhiteSpace(record.Reason))
        {
            throw new InvalidOperationException("请填写申请说明。");
        }

        record.ProofMaterialNote = NormalizeInboundProofMaterialNote(record.ProofMaterialNote);
        record.ProvideUnit = NetworkTransferDomainValues.ResolveInboundProvideUnit(record.SourceKind, record.ProvideUnit);

        if (NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind)
            && (!record.SourceResultSetId.HasValue || record.SourceResultSetId.Value <= 0))
        {
            throw new InvalidOperationException("立档资料入网必须挂接电子资料检索结果集。");
        }
    }

    private static void ValidateOutboundHeader(NetworkOutboundRecord record)
    {
        if (!NetworkTransferDomainValues.IsAllowedOutboundDestinationKind(record.DestinationKind))
        {
            throw new InvalidOperationException("请选择有效的出网目的地。");
        }

        if (string.IsNullOrWhiteSpace(record.Reason))
        {
            throw new InvalidOperationException("请填写申请说明。");
        }

        record.ProofMaterialNote = NormalizeOutboundProofMaterialNote(record.ProofMaterialNote);
    }

    private static void EnsureInboundPathsReady(NetworkInboundRecord record)
    {
        if (NetworkTransferDomainValues.IsExternalOfflineSource(record.SourceKind))
        {
            if (string.IsNullOrWhiteSpace(record.TargetServerPath))
            {
                throw new InvalidOperationException("入网单缺少目标服务器路径。");
            }

            return;
        }

        foreach (var item in record.Items)
        {
            if (string.IsNullOrWhiteSpace(item.TargetServerPath))
            {
                throw new InvalidOperationException($"明细「{item.AssetName}」缺少目标服务器路径。");
            }
        }
    }

    private static string ResolveInboundTargetServerPath(
        NetworkInboundRecord draft,
        IReadOnlyList<NetworkInboundItem> builtItems)
    {
        string draftPath = draft.TargetServerPath?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(draftPath))
        {
            return draftPath;
        }

        return builtItems
            .Select(item => item.TargetServerPath?.Trim() ?? string.Empty)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
            ?? string.Empty;
    }

    private async Task CompleteExternalInboundOnNetAssetsAsync(
        NetworkInboundRecord inbound,
        string operatorName,
        DateTime now)
    {
        List<YearlyArchiveRegisterMediaItem> mediaItems = NetworkInboundOnNetAssetMappingSupport
            .EnumerateElectronicMediaItems(inbound.MediaEntries)
            .ToList();
        foreach (YearlyArchiveRegisterMediaItem mediaItem in mediaItems)
        {
            string assetNo = await GenerateNextOnNetAssetNoAsync();
            NetworkOnNetAsset asset = NetworkInboundOnNetAssetMappingSupport.CreateOnNetAsset(
                inbound,
                mediaItem,
                assetNo,
                operatorName,
                now);
            _repository.AddOnNetAsset(asset);
        }

        if (mediaItems.Count > 0)
        {
            await _repository.SaveChangesAsync();
        }
    }

    private static void EnsureInboundReturnHardDiskSlotsReady(NetworkInboundRecord record)
    {
        var errors = new List<string>();
        NetworkInboundReturnHardDiskSupport.ValidateForComplete(record, errors);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
    }

    private void ApplyInboundReturnHardDiskItems(
        NetworkInboundRecord record,
        bool returnWithInbound,
        IEnumerable<NetworkInboundReturnHardDiskItem>? returnItems)
    {
        if (!NetworkTransferDomainValues.IsExternalOfflineSource(record.SourceKind))
        {
            record.ReturnBorrowedHardDiskWithInbound = false;
            if (record.ReturnHardDiskItems.Count > 0)
            {
                _repository.RemoveInboundReturnHardDiskItems(record.ReturnHardDiskItems.ToList());
                record.ReturnHardDiskItems.Clear();
            }

            return;
        }

        record.ReturnBorrowedHardDiskWithInbound = returnWithInbound;
        if (record.ReturnHardDiskItems.Count > 0)
        {
            _repository.RemoveInboundReturnHardDiskItems(record.ReturnHardDiskItems.ToList());
            record.ReturnHardDiskItems.Clear();
        }

        var normalizedReturnItems = returnItems?.ToList() ?? [];
        if (!returnWithInbound || normalizedReturnItems.Count == 0)
        {
            return;
        }

        foreach (NetworkInboundReturnHardDiskItem item in normalizedReturnItems.OrderBy(row => row.SortOrder))
        {
            record.ReturnHardDiskItems.Add(new NetworkInboundReturnHardDiskItem
            {
                SortOrder = item.SortOrder,
                MediumId = item.MediumId,
                DiskCode = item.DiskCode?.Trim() ?? string.Empty,
                SourceApplicationId = item.SourceApplicationId,
                SourceOutboundRecordId = item.SourceOutboundRecordId,
                TargetBlankSlotLocation = item.TargetBlankSlotLocation?.Trim() ?? string.Empty,
                CreatedAt = item.CreatedAt == default ? DateTime.Now : item.CreatedAt
            });
        }
    }

    private async Task<string> GenerateNextOnNetAssetNoAsync()
    {
        string prefix = $"网-台-{DateTime.Now.Year}-";
        string? last = await _repository.GetLastOnNetAssetNoByPrefixAsync(prefix);
        int next = 1;
        if (!string.IsNullOrWhiteSpace(last) && last.Length > prefix.Length
            && int.TryParse(last[prefix.Length..], out int parsed)
            && parsed > 0)
        {
            next = parsed + 1;
        }

        return $"{prefix}{next.ToString("D4")}";
    }

    private static string BuildDistinctSummary(IEnumerable<string?> values)
    {
        var distinct = values
            .Select(item => item?.Trim() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return distinct.Count == 0 ? string.Empty : string.Join("、", distinct);
    }

    private static void RequireSigner(string? name, string label)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException($"请填写{label}。");
        }
    }

    private static void EnsureApplicant(User? user)
    {
        if (!ArchiveRegisterBusinessRules.CanSubmitApplication(user)
            && !ArchiveRegisterBusinessRules.IsArchiveAdminUser(user))
        {
            throw new InvalidOperationException("当前用户无权办理该业务。");
        }
    }

    private static void EnsureArchiveAdmin(User? user)
    {
        if (!ArchiveRegisterBusinessRules.IsArchiveAdminUser(user))
        {
            throw new InvalidOperationException("仅资料室管理员可办理审批/交接/办结。");
        }
    }

    private static void EnsureOwnerOrAdmin(int applicantUserId, User currentUser)
    {
        if (ArchiveRegisterBusinessRules.IsArchiveAdminUser(currentUser))
        {
            return;
        }

        if (applicantUserId != currentUser.Id)
        {
            throw new InvalidOperationException("仅申请人本人可操作本单。");
        }
    }

    private static string ResolveUserDisplayName(User user) =>
        string.IsNullOrWhiteSpace(user.RealName) ? user.LoginName?.Trim() ?? string.Empty : user.RealName.Trim();

    private static string NormalizeInboundProofMaterialNote(string? proofMaterialNote) =>
        ArchiveRegisterDomainValues.NormalizeProofMaterialNote(proofMaterialNote);

    private static string NormalizeOutboundProofMaterialNote(string? proofMaterialNote) =>
        ArchiveRegisterDomainValues.NormalizeProofMaterialNote(proofMaterialNote);
}
