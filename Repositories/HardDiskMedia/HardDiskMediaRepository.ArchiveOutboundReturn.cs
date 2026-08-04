using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HardDiskMedia;

/// <summary>
/// 资料出库办结后进入 HD-RTN 的硬盘待归还来源查询。
/// </summary>
public partial class HardDiskMediaRepository
{
    public async Task<List<HardDiskMediaArchiveOutboundRequisitionReturnSource>> GetArchiveOutboundRequisitionReturnSourcesAsync()
    {
        var completedReturnPairs = await _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.SourceOutboundRecordId != null)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Select(item => new { item.MediumId, OutboundRecordId = item.SourceOutboundRecordId!.Value })
            .ToListAsync();

        var completedReturnKeys = completedReturnPairs
            .Select(item => BuildArchiveOutboundRequisitionReturnKey(item.MediumId, item.OutboundRecordId))
            .ToHashSet(StringComparer.Ordinal);

        var outboundRecords = await _dbContext.YearlyArchiveOutboundRecords
            .AsNoTracking()
            .Include(record => record.Items)
            .Where(record => record.Status == YearlyArchiveOutboundRecord.Completed)
            .OrderByDescending(record => record.CompletedAt ?? record.UpdatedAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync();

        var candidateItems = outboundRecords
            .SelectMany(record => record.Items
                .Where(ArchiveOutboundReturnSupport.IsArchiveOutboundHardDiskReturnItem)
                .Select(item => new ArchiveOutboundHardDiskReturnCandidateRow(
                    record.Id,
                    record.OutboundNo,
                    record.ApplicantName,
                    record.ApplicantDept,
                    record.ExpectedReturnDate,
                    item)))
            .ToList();

        if (candidateItems.Count == 0)
        {
            return [];
        }

        var mediumIdsByCandidateKey = await ResolveArchiveOutboundHardDiskMediumIdsAsync(candidateItems);
        var allMediumIds = mediumIdsByCandidateKey.Values
            .SelectMany(ids => ids)
            .Distinct()
            .ToList();

        if (allMediumIds.Count == 0)
        {
            return [];
        }

        var mediaById = await _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(medium => medium.Ledger)
            .Where(medium => allMediumIds.Contains(medium.Id) && !medium.IsDeleted)
            .ToDictionaryAsync(medium => medium.Id);

        var outboundNos = candidateItems
            .Select(item => item.OutboundNo)
            .Where(no => !string.IsNullOrWhiteSpace(no))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var archiveOutboundTransactions = await _dbContext.HardDiskMediaTransactions
            .AsNoTracking()
            .Where(transaction => allMediumIds.Contains(transaction.MediumId))
            .Where(transaction => transaction.ApplicationId == null)
            .Where(transaction => outboundNos.Contains(transaction.RelatedBatch))
            .OrderByDescending(transaction => transaction.OperateTime)
            .ThenByDescending(transaction => transaction.Id)
            .ToListAsync();

        var transactionLookup = archiveOutboundTransactions
            .GroupBy(transaction => BuildArchiveOutboundRequisitionReturnKey(transaction.MediumId, transaction.RelatedBatch))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var results = new List<HardDiskMediaArchiveOutboundRequisitionReturnSource>();
        var seenMediumIds = new HashSet<int>();

        foreach (var candidate in candidateItems)
        {
            if (!mediumIdsByCandidateKey.TryGetValue(BuildCandidateItemKey(candidate), out var mediumIds)
                || mediumIds.Count == 0)
            {
                continue;
            }

            foreach (int mediumId in mediumIds)
            {
                if (!seenMediumIds.Add(mediumId))
                {
                    continue;
                }

                if (completedReturnKeys.Contains(BuildArchiveOutboundRequisitionReturnKey(mediumId, candidate.OutboundRecordId)))
                {
                    continue;
                }

                if (!mediaById.TryGetValue(mediumId, out var medium) || medium.Ledger == null)
                {
                    continue;
                }

                string currentStatus = medium.Ledger.MediaStatus?.Trim() ?? string.Empty;
                if (!medium.Ledger.NeedReturn
                    || (currentStatus != HardDiskMedium.StatusOutTemporary
                        && currentStatus != HardDiskMedium.StatusOutLongTerm))
                {
                    continue;
                }

                string transactionKey = BuildArchiveOutboundRequisitionReturnKey(mediumId, candidate.OutboundNo);
                transactionLookup.TryGetValue(transactionKey, out var outboundTransaction);

                results.Add(new HardDiskMediaArchiveOutboundRequisitionReturnSource
                {
                    OutboundRecordId = candidate.OutboundRecordId,
                    OutboundNo = candidate.OutboundNo,
                    ApplicantName = candidate.ApplicantName?.Trim() ?? string.Empty,
                    ApplicantDept = candidate.ApplicantDept?.Trim() ?? string.Empty,
                    MediumId = mediumId,
                    DiskCode = medium.DiskCode,
                    SerialNumber = medium.SerialNumber,
                    Capacity = medium.Capacity,
                    InterfaceType = medium.InterfaceType,
                    BorrowedLocation = medium.Ledger.StorageLocation?.Trim() ?? string.Empty,
                    OriginalLocation = outboundTransaction?.BeforeLocation?.Trim() ?? string.Empty,
                    CurrentStatus = currentStatus,
                    ExpectedReturnDate = candidate.Item.ExpectedReturnDate ?? candidate.ExpectedReturnDate
                });
            }
        }

        return results;
    }

    private async Task<Dictionary<string, List<int>>> ResolveArchiveOutboundHardDiskMediumIdsAsync(
        IReadOnlyList<ArchiveOutboundHardDiskReturnCandidateRow> candidateItems)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        foreach (var candidate in candidateItems)
        {
            string key = BuildCandidateItemKey(candidate);
            if (candidate.Item.RequisitionedMediumId is > 0
                && ArchiveOutboundReturnSupport.IsArchiveOutboundRequisitionReturnItem(candidate.Item))
            {
                result[key] = [candidate.Item.RequisitionedMediumId.Value];
            }
            else
            {
                result[key] = [];
            }
        }

        var filedCandidates = candidateItems
            .Where(item => ArchiveOutboundReturnSupport.IsArchiveOutboundFiledHardDiskReturnItem(item.Item))
            .ToList();

        if (filedCandidates.Count == 0)
        {
            return result;
        }

        var factIds = filedCandidates
            .Select(item => item.Item.FilingFactId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var facts = factIds.Count == 0
            ? []
            : await _dbContext.YearlyArchiveFilingFacts
                .AsNoTracking()
                .Where(fact => factIds.Contains(fact.Id))
                .Select(fact => new { fact.Id, fact.ContainerId, fact.MediumCode, fact.ContainerCode })
                .ToListAsync();

        var factsById = facts.ToDictionary(fact => fact.Id);

        var unitIds = facts
            .Where(fact => fact.ContainerId > 0)
            .Select(fact => fact.ContainerId)
            .Distinct()
            .ToList();

        var linksByUnitId = unitIds.Count == 0
            ? new Dictionary<int, List<int>>()
            : (await _dbContext.YearlyElectronicArchiveUnitMediumLinks
                    .AsNoTracking()
                    .Where(link => unitIds.Contains(link.YearlyElectronicArchiveUnitId))
                    .Select(link => new { link.YearlyElectronicArchiveUnitId, link.HardDiskMediumId })
                    .ToListAsync())
                .GroupBy(link => link.YearlyElectronicArchiveUnitId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(link => link.HardDiskMediumId).Distinct().ToList());

        var codeCandidates = facts
            .SelectMany(fact => new[] { fact.MediumCode, fact.ContainerCode })
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mediaByCode = codeCandidates.Count == 0
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : (await _dbContext.HardDiskMedia
                    .AsNoTracking()
                    .Where(medium => !medium.IsDeleted && codeCandidates.Contains(medium.DiskCode))
                    .Select(medium => new { medium.DiskCode, medium.Id })
                    .ToListAsync())
                .GroupBy(medium => medium.DiskCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in filedCandidates)
        {
            string key = BuildCandidateItemKey(candidate);
            var mediumIds = new List<int>();

            if (factsById.TryGetValue(candidate.Item.FilingFactId, out var fact))
            {
                if (fact.ContainerId > 0
                    && linksByUnitId.TryGetValue(fact.ContainerId, out var linkedIds))
                {
                    mediumIds.AddRange(linkedIds);
                }

                if (mediumIds.Count == 0
                    && !string.IsNullOrWhiteSpace(fact.MediumCode)
                    && mediaByCode.TryGetValue(fact.MediumCode.Trim(), out int mediumIdByCode))
                {
                    mediumIds.Add(mediumIdByCode);
                }

                if (mediumIds.Count == 0
                    && !string.IsNullOrWhiteSpace(fact.ContainerCode)
                    && mediaByCode.TryGetValue(fact.ContainerCode.Trim(), out int mediumIdByContainer))
                {
                    mediumIds.Add(mediumIdByContainer);
                }
            }

            result[key] = mediumIds.Distinct().ToList();
        }

        var unresolvedFiled = filedCandidates
            .Where(candidate => result[BuildCandidateItemKey(candidate)].Count == 0)
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.OutboundNo))
            .ToList();

        if (unresolvedFiled.Count == 0)
        {
            return result;
        }

        var unresolvedOutboundNos = unresolvedFiled
            .Select(candidate => candidate.OutboundNo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var transactionRows = await _dbContext.HardDiskMediaTransactions
            .AsNoTracking()
            .Where(transaction => transaction.ApplicationId == null)
            .Where(transaction => unresolvedOutboundNos.Contains(transaction.RelatedBatch))
            .Select(transaction => new { transaction.RelatedBatch, transaction.MediumId })
            .ToListAsync();

        var mediumIdsByOutboundNo = transactionRows
            .GroupBy(row => row.RelatedBatch.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.MediumId).Distinct().ToList(),
                StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in unresolvedFiled)
        {
            if (mediumIdsByOutboundNo.TryGetValue(candidate.OutboundNo.Trim(), out var mediumIds))
            {
                result[BuildCandidateItemKey(candidate)] = mediumIds;
            }
        }

        return result;
    }

    private static string BuildCandidateItemKey(ArchiveOutboundHardDiskReturnCandidateRow candidate) =>
        $"{candidate.OutboundRecordId}:{candidate.Item.Id}";

    private static string BuildArchiveOutboundRequisitionReturnKey(int mediumId, int outboundRecordId) =>
        $"{mediumId}:{outboundRecordId}";

    private static string BuildArchiveOutboundRequisitionReturnKey(int mediumId, string outboundNo) =>
        $"{mediumId}:{outboundNo.Trim()}";

    private sealed record ArchiveOutboundHardDiskReturnCandidateRow(
        int OutboundRecordId,
        string OutboundNo,
        string? ApplicantName,
        string? ApplicantDept,
        DateTime? ExpectedReturnDate,
        YearlyArchiveOutboundItem Item);
}
