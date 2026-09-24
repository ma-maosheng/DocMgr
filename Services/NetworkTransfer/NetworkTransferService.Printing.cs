using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;
using DocMgr.Services.SystemSettings;

namespace DocMgr.Services.NetworkTransfer;

/// <summary>
/// 入网申请单打印数据装配。
/// </summary>
public sealed partial class NetworkTransferService
{
    private const string BlankDateText = "______年___月___日";

    public async Task<NetworkInboundPrintData> BuildInboundPrintDataAsync(int recordId, bool blankApprovalSignatures)
    {
        var record = await _repository.GetInboundByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        string serverPathName = !string.IsNullOrWhiteSpace(record.TargetServerPath)
            ? record.TargetServerPath.Trim()
            : record.Items
                .Select(item => item.TargetServerPath?.Trim() ?? string.Empty)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path))
                ?? string.Empty;

        string serverPhysicalPath = ResolveServerPhysicalPath(serverPathName);
        NetworkInboundItemPrintContext itemPrintContext = await BuildInboundItemPrintContextAsync(record);

        return BuildInboundPrintData(
            record,
            blankApprovalSignatures,
            serverPathName,
            serverPhysicalPath,
            itemPrintContext,
            await ResolveInboundApprovalChainAsync(record));
    }

    private async Task<NetworkInboundItemPrintContext> BuildInboundItemPrintContextAsync(NetworkInboundRecord record)
    {
        if (!NetworkTransferDomainValues.IsArchivedElectronicSearchSource(record.SourceKind))
        {
            return NetworkInboundItemPrintContext.Empty;
        }

        List<int> factIds = record.Items
            .Where(item => item.SourceFilingFactId is > 0)
            .Select(item => item.SourceFilingFactId!.Value)
            .Distinct()
            .ToList();
        if (factIds.Count == 0)
        {
            return NetworkInboundItemPrintContext.Empty;
        }

        IReadOnlyDictionary<int, FiledArchiveSearchHit> hitsByFactId =
            await _archiveFilingSearchService.GetSearchHitsByFilingFactIdsAsync(factIds);

        Dictionary<int, YearlyArchiveSearchResultSetItem> resultSetItemsById = new();
        if (record.SourceResultSetId is int resultSetId && resultSetId > 0)
        {
            YearlyArchiveSearchResultSet? resultSet = await _archiveFilingSearchService.GetSearchPoolByIdAsync(resultSetId);
            if (resultSet?.Items != null)
            {
                foreach (YearlyArchiveSearchResultSetItem item in resultSet.Items.Where(item => item.Id > 0))
                {
                    resultSetItemsById.TryAdd(item.Id, item);
                }
            }
        }

        return new NetworkInboundItemPrintContext
        {
            HitsByFactId = hitsByFactId,
            ResultSetItemsById = resultSetItemsById
        };
    }

    public async Task RecordInboundPrintAsync(int recordId)
    {
        var record = await _repository.GetInboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到入网申请单。");

        int printCount = record.PrintCount;
        DateTime? firstPrintedAt = record.FirstPrintedAt;
        DateTime? lastPrintedAt = record.LastPrintedAt;
        ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt);
        record.PrintCount = printCount;
        record.FirstPrintedAt = firstPrintedAt;
        record.LastPrintedAt = lastPrintedAt;
        record.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    public async Task<NetworkOutboundPrintData> BuildOutboundPrintDataAsync(int recordId, bool blankApprovalSignatures)
    {
        var record = await _repository.GetOutboundByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        // 以数据库状态为准：已审批及之后预填审批签字；已确认实物交接后预填交接签字。
        bool effectiveBlankApproval = record.Status < NetworkOutboundRecord.StatusApproved;
        return BuildOutboundPrintData(
            record,
            effectiveBlankApproval,
            await ResolveOutboundApprovalChainAsync(record));
    }

    public async Task RecordOutboundPrintAsync(int recordId)
    {
        var record = await _repository.GetOutboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        int printCount = record.PrintCount;
        DateTime? firstPrintedAt = record.FirstPrintedAt;
        DateTime? lastPrintedAt = record.LastPrintedAt;
        ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt);
        record.PrintCount = printCount;
        record.FirstPrintedAt = firstPrintedAt;
        record.LastPrintedAt = lastPrintedAt;
        record.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    private static NetworkInboundPrintData BuildInboundPrintData(
        NetworkInboundRecord record,
        bool blankApprovalSignatures,
        string serverPathName,
        string serverPhysicalPath,
        NetworkInboundItemPrintContext itemPrintContext,
        ApprovalChainResolution chain)
    {
        if (record.Status < NetworkInboundRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("请先提交申请后再打印。");
        }

        string applyDate = record.ApplyTime == default
            ? string.Empty
            : record.ApplyTime.ToString("yyyy-MM-dd");

        string proofMaterial = ArchiveRegisterDomainValues.HasProofMaterial(record.ProofMaterialNote)
            ? record.ProofMaterialNote.Trim()
            : ArchiveRegisterDomainValues.ProofMaterialNoneText;

        bool blankHandover = !record.HandoverConfirmedAt.HasValue
                             && record.Status != NetworkInboundRecord.StatusCompleted;

        return new NetworkInboundPrintData
        {
            InboundNo = record.InboundNo,
            ApplyDateText = applyDate,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            YearText = record.Year?.Trim() ?? string.Empty,
            ProjectName = record.ProjectName?.Trim() ?? string.Empty,
            MaterialName = record.MaterialName?.Trim() ?? string.Empty,
            SourceKindText = NetworkTransferDomainValues.NormalizeSourceKind(record.SourceKind),
            ProvideUnitText = NetworkTransferDomainValues.ResolveInboundProvideUnit(record.SourceKind, record.ProvideUnit),
            Reason = record.Reason?.Trim() ?? string.Empty,
            OtherRequests = record.OtherRequests?.Trim() ?? string.Empty,
            ProofMaterialNote = proofMaterial,
            ReturnBorrowedHardDiskText = NetworkInboundReturnHardDiskPrintSupport.BuildReturnHardDiskDescription(record) ?? string.Empty,
            ServerPath = string.IsNullOrWhiteSpace(serverPathName) ? "(未指定)" : serverPathName,
            ServerPhysicalPath = serverPhysicalPath,
            ItemLines = NetworkInboundItemPrintSupport.BuildItemLines(record, itemPrintContext).ToList(),
            EnableDeptHead = chain.DeptHead.IsEnabled,
            EnableProductionHead = chain.ProductionHead.IsEnabled,
            EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
            EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
            EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
            DeptHeadBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.DeptHead,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptHeadDate)),
            ProductionHeadBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ProductionHead,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionHeadDate)),
            ArchiveRoomHeadBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ArchiveRoomHead,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveRoomHeadDate)),
            ArchiveDeputyPresidentBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ArchiveDeputyPresident,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveDeputyPresidentDate)),
            ProductionVicePresidentBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ProductionVicePresident,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionVicePresidentDate)),
            HandoverSignatureBlock = blankHandover
                ? BuildBlankInboundHandoverSignatureBlock()
                : BuildFilledInboundHandoverSignatureBlock(record),
            PrintCount = record.PrintCount
        };
    }

    private static string BuildApprovalBlock(string signer, string dateText) =>
        PrintApprovalSignatureSupport.FormatInline(signer, dateText);

    private static string BuildBlankInboundHandoverSignatureBlock() =>
        "移交人签字：                                            日期:______年___月___日\n" +
        "资料员签字：                                           日期:______年___月___日";

    private static string BuildFilledInboundHandoverSignatureBlock(NetworkInboundRecord record)
    {
        string deliverer = string.IsNullOrWhiteSpace(record.Deliverer) ? "________________" : record.Deliverer.Trim();
        string administrator = string.IsNullOrWhiteSpace(record.Administrator) ? "________________" : record.Administrator.Trim();
        string deliverDate = FormatDate(record.DeliverDate);
        string adminDate = FormatDate(record.AdminDate);

        return $"移交人签字：{deliverer}    日期：{deliverDate}\n" +
               $"资料员签字：{administrator}    日期：{adminDate}";
    }

    private static NetworkOutboundPrintData BuildOutboundPrintData(
        NetworkOutboundRecord record,
        bool blankApprovalSignatures,
        ApprovalChainResolution chain)
    {
        if (record.Status < NetworkOutboundRecord.StatusSubmitted)
        {
            throw new InvalidOperationException("请先提交申请后再打印。");
        }

        string applyDate = record.ApplyTime == default
            ? string.Empty
            : record.ApplyTime.ToString("yyyy-MM-dd");

        string proofMaterial = ArchiveRegisterDomainValues.HasProofMaterial(record.ProofMaterialNote)
            ? record.ProofMaterialNote.Trim()
            : ArchiveRegisterDomainValues.ProofMaterialNoneText;

        // 办结前交接签字留白；已确认实物交接或已办结时从库中预填。
        bool blankHandover = !record.HandoverConfirmedAt.HasValue
                             && record.Status != NetworkOutboundRecord.StatusCompleted;

        return new NetworkOutboundPrintData
        {
            OutboundNo = record.OutboundNo,
            ApplyDateText = applyDate,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            YearText = record.Year?.Trim() ?? string.Empty,
            ProjectName = record.ProjectName?.Trim() ?? string.Empty,
            DestinationKindText = record.DestinationKind?.Trim() ?? string.Empty,
            ArchivePurposeText = record.ArchivePurpose?.Trim() ?? string.Empty,
            Reason = record.Reason?.Trim() ?? string.Empty,
            ProofMaterialNote = proofMaterial,
            ItemLines = NetworkOutboundItemPrintSupport.BuildItemLines(record).ToList(),
            HasPendingItemDetailCapture = NetworkOutboundItemPrintSupport.HasPendingItemDetailCapture(record.MediaEntries),
            EnableDeptHead = chain.DeptHead.IsEnabled,
            EnableProductionHead = chain.ProductionHead.IsEnabled,
            EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
            EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
            EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
            DeptHeadBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.DeptHead,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptHeadDate)),
            ProductionHeadBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ProductionHead,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionHeadDate)),
            ArchiveRoomHeadBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ArchiveRoomHead,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveRoomHeadDate)),
            ArchiveDeputyPresidentBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ArchiveDeputyPresident,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ArchiveDeputyPresidentDate)),
            ProductionVicePresidentBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ProductionVicePresident,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ProductionVicePresidentDate)),
            HandoverSignatureBlock = blankHandover
                ? BuildBlankOutboundHandoverSignatureBlock()
                : BuildFilledOutboundHandoverSignatureBlock(record),
            PrintCount = record.PrintCount
        };
    }

    private static string BuildBlankOutboundHandoverSignatureBlock() =>
        "移交人签字：                                            日期:______年___月___日\n" +
        "资料员签字：                                           日期:______年___月___日";

    private static string BuildFilledOutboundHandoverSignatureBlock(NetworkOutboundRecord record)
    {
        string deliverer = string.IsNullOrWhiteSpace(record.Deliverer) ? "________________" : record.Deliverer.Trim();
        string administrator = string.IsNullOrWhiteSpace(record.Administrator) ? "________________" : record.Administrator.Trim();
        string deliverDate = FormatDate(record.DeliverDate);
        string adminDate = FormatDate(record.AdminDate);

        return $"移交人签字：{deliverer}    日期：{deliverDate}\n" +
               $"资料员签字：{administrator}    日期：{adminDate}";
    }

    private string ResolveServerPhysicalPath(string pathNameOrPhysicalPath)
    {
        string pathText = pathNameOrPhysicalPath?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pathText))
        {
            return string.Empty;
        }

        foreach (ServerPathSetting setting in _serverPathSettingService.GetAll())
        {
            string pathName = setting.PathName?.Trim() ?? string.Empty;
            string physicalPath = setting.PhysicalPath?.Trim() ?? string.Empty;
            if (string.Equals(pathName, pathText, StringComparison.Ordinal)
                || string.Equals(physicalPath, pathText, StringComparison.Ordinal))
            {
                return physicalPath;
            }
        }

        return string.Empty;
    }

    public async Task<NetworkOnNetDisposalPrintData> BuildDisposalPrintDataAsync(int recordId)
    {
        var record = await _repository.GetDisposalByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (record.Status is NetworkOnNetDisposalRecord.StatusDraft
            or NetworkOnNetDisposalRecord.StatusWithdrawn
            or NetworkOnNetDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("请先提交处置单后再打印。");
        }

        IReadOnlyList<NetworkOnNetDisposalItem> orderedItems = record.Items
            .OrderBy(item => item.SortOrder)
            .ToList();
        Dictionary<int, NetworkOnNetAsset> assetsById = (await GetOnNetAssetsByIdsAsync(
                orderedItems.Select(item => item.OnNetAssetId).Where(id => id > 0).Distinct().ToList()))
            .ToDictionary(item => item.Id);

        var chain = await _approvalWorkflowService.ResolveAsync(
            new ApprovalChainResolveRequest
            {
                BusinessType = ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal,
                FieldValues = ApprovalChainApplySupport.BuildNetworkOnNetDisposalFieldValues(record)
            },
            _userService.GetAllUsers());

        return new NetworkOnNetDisposalPrintData
        {
            DisposalNo = record.DisposalNo,
            ApplyDateText = record.ApplyTime == default
                ? string.Empty
                : record.ApplyTime.ToString("yyyy-MM-dd"),
            DisposalReason = FirstNonEmpty(
                record.DisposalReason,
                BuildDistinctSummary(orderedItems.Select(item => item.DisposalReason))),
            DispositionMethod = FirstNonEmpty(
                record.DispositionMethod,
                BuildDistinctSummary(orderedItems.Select(item => item.DispositionMethod))),
            Reason = record.Reason,
            Remark = record.Remark,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            DeptHead = record.DeptHead,
            DeptHeadDateText = record.DeptHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ArchiveRoomHead = record.ArchiveRoomHead,
            ArchiveRoomHeadDateText = record.ArchiveRoomHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductionHead = record.ProductionHead,
            ProductionHeadDateText = record.ProductionHeadDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ArchiveDeputyPresident = record.ArchiveDeputyPresident,
            ArchiveDeputyPresidentDateText = record.ArchiveDeputyPresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            ProductionVicePresident = record.ProductionVicePresident,
            ProductionVicePresidentDateText = record.ProductionVicePresidentDate?.ToString("yyyy-MM-dd") ?? string.Empty,
            EnableDeptHead = chain.DeptHead.IsEnabled,
            EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
            EnableProductionHead = chain.ProductionHead.IsEnabled,
            EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
            EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled,
            CompletedBy = record.CompletedBy,
            CompletedDateText = record.CompletedAt?.ToString("yyyy-MM-dd") ?? string.Empty,
            IsCompleted = record.Status == NetworkOnNetDisposalRecord.StatusCompleted,
            PrintCount = record.PrintCount,
            Items = orderedItems
                .Select(item => BuildDisposalPrintItem(item, assetsById))
                .ToList()
        };
    }

    public async Task RecordDisposalPrintAsync(int recordId)
    {
        var record = await _repository.GetDisposalByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到在网处置单。");

        if (record.Status is NetworkOnNetDisposalRecord.StatusDraft
            or NetworkOnNetDisposalRecord.StatusWithdrawn
            or NetworkOnNetDisposalRecord.StatusForceWithdrawn)
        {
            throw new InvalidOperationException("当前状态不可打印签批单。");
        }

        DateTime now = DateTime.Now;
        int printCount = record.PrintCount;
        DateTime? firstPrintedAt = record.FirstPrintedAt;
        DateTime? lastPrintedAt = record.LastPrintedAt;
        ApprovalSignatureDateSupport.RecordPrint(ref printCount, ref firstPrintedAt, ref lastPrintedAt, now);
        record.PrintCount = printCount;
        record.FirstPrintedAt = firstPrintedAt;
        record.LastPrintedAt = lastPrintedAt;
        record.UpdatedAt = now;
        await _repository.SaveChangesAsync();
    }

    private static NetworkOnNetDisposalPrintItemData BuildDisposalPrintItem(
        NetworkOnNetDisposalItem item,
        IReadOnlyDictionary<int, NetworkOnNetAsset> assetsById)
    {
        assetsById.TryGetValue(item.OnNetAssetId, out NetworkOnNetAsset? asset);
        string materialName = FirstNonEmpty(asset?.MaterialName, asset?.AssetName, item.AssetName);
        return new NetworkOnNetDisposalPrintItemData
        {
            SortOrder = item.SortOrder,
            AssetNo = FirstNonEmpty(item.AssetNo, asset?.AssetNo),
            Year = asset?.Year?.Trim() ?? string.Empty,
            ProjectName = asset?.ProjectName?.Trim() ?? string.Empty,
            MaterialName = materialName,
            AssetKind = FirstNonEmpty(item.AssetKind, asset?.AssetKind),
            ServerPath = FirstNonEmpty(item.ServerPath, asset?.ServerPath),
            BeforeLifecycleStatus = FirstNonEmpty(item.BeforeLifecycleStatus, asset?.LifecycleStatus),
            DisposalReason = item.DisposalReason?.Trim() ?? string.Empty,
            DispositionMethod = item.DispositionMethod?.Trim() ?? string.Empty
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (string? value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd") : BlankDateText;
}
