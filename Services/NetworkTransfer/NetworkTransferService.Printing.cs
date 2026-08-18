using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.Interfaces;

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

        return BuildInboundPrintData(record, blankApprovalSignatures, serverPathName, serverPhysicalPath, itemPrintContext);
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

        record.PrintCount++;
        record.LastPrintedAt = DateTime.Now;
        record.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    public async Task<NetworkOutboundPrintData> BuildOutboundPrintDataAsync(int recordId, bool blankApprovalSignatures)
    {
        var record = await _repository.GetOutboundByIdAsync(recordId)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        // 以数据库状态为准：已审批及之后预填审批签字；已确认实物交接后预填交接签字。
        bool effectiveBlankApproval = record.Status < NetworkOutboundRecord.StatusApproved;
        return BuildOutboundPrintData(record, effectiveBlankApproval);
    }

    public async Task RecordOutboundPrintAsync(int recordId)
    {
        var record = await _repository.GetOutboundByIdAsync(recordId, tracking: true)
            ?? throw new InvalidOperationException("未找到出网申请单。");

        record.PrintCount++;
        record.LastPrintedAt = DateTime.Now;
        record.UpdatedAt = DateTime.Now;
        await _repository.SaveChangesAsync();
    }

    private static NetworkInboundPrintData BuildInboundPrintData(
        NetworkInboundRecord record,
        bool blankApprovalSignatures,
        string serverPathName,
        string serverPhysicalPath,
        NetworkInboundItemPrintContext itemPrintContext)
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
            DeptLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.DeptLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptDate)),
            ProdLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ProdLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ProdDate)),
            RndLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.RndLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.RndDate)),
            DeputyLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.DeputyLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.DeputyDate)),
            HandoverSignatureBlock = blankHandover
                ? BuildBlankInboundHandoverSignatureBlock()
                : BuildFilledInboundHandoverSignatureBlock(record),
            PrintCount = record.PrintCount
        };
    }

    private static string BuildApprovalBlock(string signer, string dateText)
    {
        string signatureSlot = string.IsNullOrWhiteSpace(signer) ? "________________" : signer.Trim();
        string renderedDate = string.IsNullOrWhiteSpace(dateText) ? BlankDateText : dateText;
        return $"签字：{signatureSlot}    日期：{renderedDate}";
    }

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
        bool blankApprovalSignatures)
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
            DeptLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.DeptLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.DeptDate)),
            ProdLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.ProdLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.ProdDate)),
            RndLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.RndLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.RndDate)),
            DeputyLeaderLabel = NetworkTransferDomainValues.ResolveOutboundDeputyLeaderRole(record.DestinationKind),
            DeputyLeaderBlock = BuildApprovalBlock(
                blankApprovalSignatures ? string.Empty : record.DeputyLeader,
                blankApprovalSignatures ? BlankDateText : FormatDate(record.DeputyDate)),
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
            ApprovedBy = record.ApprovedBy,
            ApprovedDateText = record.ApprovedTime?.ToString("yyyy-MM-dd") ?? string.Empty,
            ApprovalOpinion = record.ApprovalOpinion,
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
        record.PrintCount++;
        record.LastPrintedAt = now;
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
