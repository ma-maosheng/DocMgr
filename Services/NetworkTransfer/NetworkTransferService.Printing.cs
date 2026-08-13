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

        return BuildOutboundPrintData(record, blankApprovalSignatures);
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

        // 办结前交接签字留白；已办结预填。
        bool blankHandover = record.Status != NetworkOutboundRecord.StatusCompleted;

        return new NetworkOutboundPrintData
        {
            OutboundNo = record.OutboundNo,
            ApplyDateText = applyDate,
            ApplicantName = record.ApplicantName,
            ApplicantDept = record.ApplicantDept,
            YearText = record.Year?.Trim() ?? string.Empty,
            ProjectName = record.ProjectName?.Trim() ?? string.Empty,
            DestinationKindText = record.DestinationKind?.Trim() ?? string.Empty,
            Reason = record.Reason?.Trim() ?? string.Empty,
            ProofMaterialNote = proofMaterial,
            ItemLines = NetworkOutboundItemPrintSupport.BuildItemLines(record.Items).ToList(),
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

    private static string FormatDate(DateTime? value) =>
        value.HasValue ? value.Value.ToString("yyyy-MM-dd") : BlankDateText;
}
