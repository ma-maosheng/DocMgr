using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.NetworkTransfer;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.NetworkTransfer;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.HardDiskMedia;

/// <summary>
/// 出网办结后进入 HD-RTN 的库内空盘征用待归还来源查询。
/// </summary>
public partial class HardDiskMediaRepository
{
    public async Task<List<HardDiskMediaNetworkOutboundRequisitionReturnSource>> GetNetworkOutboundRequisitionReturnSourcesAsync()
    {
        var completedReturnPairs = await _dbContext.HardDiskMediaApplications
            .AsNoTracking()
            .Where(item => item.SourceNetworkOutboundRecordId != null)
            .Where(item => item.ApplicationStatus == HardDiskMediaApplication.StatusCompleted)
            .Where(item => item.ApplicationType == HardDiskMediaApplication.TypeReturnBlankRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDataRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeReturnDamagedRegistration
                           || item.ApplicationType == HardDiskMediaApplication.TypeLossRegistration)
            .Select(item => new { item.MediumId, OutboundRecordId = item.SourceNetworkOutboundRecordId!.Value })
            .ToListAsync();

        var completedReturnKeys = completedReturnPairs
            .Select(item => BuildNetworkOutboundRequisitionReturnKey(item.MediumId, item.OutboundRecordId))
            .ToHashSet(StringComparer.Ordinal);

        var outboundRecords = await _dbContext.NetworkOutboundRecords
            .AsNoTracking()
            .Include(record => record.MediaEntries)
            .Where(record => record.Status == NetworkOutboundRecord.StatusCompleted)
            .OrderByDescending(record => record.CompletedAt ?? record.UpdatedAt)
            .ThenByDescending(record => record.Id)
            .ToListAsync();

        var candidateMedias = outboundRecords
            .SelectMany(record => NetworkOutboundExternalHardDiskRequisitionSupport
                .EnumerateBlankHardDiskRequisitions(record.MediaEntries)
                .Where(media => media.RequisitionedDiskNeedReturn)
                .Select(media => new NetworkOutboundHardDiskReturnCandidateRow(
                    record.Id,
                    record.OutboundNo,
                    record.ApplicantName,
                    record.ApplicantDept,
                    media)))
            .ToList();

        if (candidateMedias.Count == 0)
        {
            return [];
        }

        var allMediumIds = candidateMedias
            .Select(item => item.Media.RequisitionedMediumId!.Value)
            .Distinct()
            .ToList();

        var mediaById = await _dbContext.HardDiskMedia
            .AsNoTracking()
            .Include(medium => medium.Ledger)
            .Where(medium => allMediumIds.Contains(medium.Id) && !medium.IsDeleted)
            .ToDictionaryAsync(medium => medium.Id);

        var outboundNos = candidateMedias
            .Select(item => item.OutboundNo)
            .Where(no => !string.IsNullOrWhiteSpace(no))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var outboundTransactions = await _dbContext.HardDiskMediaTransactions
            .AsNoTracking()
            .Where(transaction => allMediumIds.Contains(transaction.MediumId))
            .Where(transaction => transaction.ApplicationId == null)
            .Where(transaction => outboundNos.Contains(transaction.RelatedBatch))
            .OrderByDescending(transaction => transaction.OperateTime)
            .ThenByDescending(transaction => transaction.Id)
            .ToListAsync();

        var transactionLookup = outboundTransactions
            .GroupBy(transaction => BuildNetworkOutboundRequisitionReturnKey(transaction.MediumId, transaction.RelatedBatch))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var results = new List<HardDiskMediaNetworkOutboundRequisitionReturnSource>();
        var seenMediumIds = new HashSet<int>();

        foreach (var candidate in candidateMedias)
        {
            int mediumId = candidate.Media.RequisitionedMediumId!.Value;
            if (!seenMediumIds.Add(mediumId))
            {
                continue;
            }

            if (completedReturnKeys.Contains(BuildNetworkOutboundRequisitionReturnKey(mediumId, candidate.OutboundRecordId)))
            {
                continue;
            }

            if (!mediaById.TryGetValue(mediumId, out HardDiskMedium? medium) || medium.Ledger == null)
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

            string transactionKey = BuildNetworkOutboundRequisitionReturnKey(mediumId, candidate.OutboundNo);
            transactionLookup.TryGetValue(transactionKey, out HardDiskMediaTransaction? outboundTransaction);

            results.Add(new HardDiskMediaNetworkOutboundRequisitionReturnSource
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
                ExpectedReturnDate = candidate.Media.ExpectedReturnDate
            });
        }

        return results;
    }

    public Task<string?> GetNetworkOutboundNoByRecordIdAsync(int outboundRecordId)
    {
        return _dbContext.NetworkOutboundRecords
            .AsNoTracking()
            .Where(record => record.Id == outboundRecordId)
            .Select(record => record.OutboundNo)
            .FirstOrDefaultAsync();
    }

    private static string BuildNetworkOutboundRequisitionReturnKey(int mediumId, int outboundRecordId) =>
        $"{mediumId}:NW-OUT:{outboundRecordId}";

    private static string BuildNetworkOutboundRequisitionReturnKey(int mediumId, string outboundNo) =>
        $"{mediumId}:NW-OUT-NO:{outboundNo?.Trim() ?? string.Empty}";

    private sealed record NetworkOutboundHardDiskReturnCandidateRow(
        int OutboundRecordId,
        string OutboundNo,
        string ApplicantName,
        string ApplicantDept,
        YearlyArchiveRegisterMedia Media);
}
