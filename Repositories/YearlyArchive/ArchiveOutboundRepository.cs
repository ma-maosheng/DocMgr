using DocMgr.Data;
using DocMgr.Models.ArchiveContainers;
using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed class ArchiveOutboundRepository : IArchiveOutboundRepository
    {
        private sealed class ArchiveOutboundRepositoryTransaction : IArchiveFilingRepositoryTransaction
        {
            private readonly IDbContextTransaction _transaction;

            public ArchiveOutboundRepositoryTransaction(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public Task CommitAsync() => _transaction.CommitAsync();

            public Task RollbackAsync() => _transaction.RollbackAsync();

            public async ValueTask DisposeAsync() => await _transaction.DisposeAsync();
        }

        private readonly AppDbContext _dbContext;

        public ArchiveOutboundRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync()
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync();
            return new ArchiveOutboundRepositoryTransaction(transaction);
        }

        public Task<YearlyArchiveOutboundRecord?> GetByIdWithDetailsAsync(int id)
        {
            return _dbContext.YearlyArchiveOutboundRecords
                .Include(record => record.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
                .Include(record => record.SyncEntries)
                .FirstOrDefaultAsync(record => record.Id == id);
        }

        public Task<YearlyArchiveOutboundRecord?> GetByOutboundNoWithDetailsAsync(string outboundNo)
        {
            string normalized = outboundNo.Trim();
            return _dbContext.YearlyArchiveOutboundRecords
                .Include(record => record.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
                .Include(record => record.SyncEntries)
                .FirstOrDefaultAsync(record => record.OutboundNo == normalized);
        }

        public Task<List<YearlyArchiveOutboundRecord>> ListByYearAsync(int year)
        {
            return _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Include(record => record.Items)
                .Where(record => record.ApplyDate.Year == year)
                .OrderByDescending(record => record.OutboundNo)
                .ToListAsync();
        }

        public Task<List<YearlyArchiveOutboundRecord>> ListByApplicantUserIdAsync(int userId, int year)
        {
            return _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Include(record => record.Items)
                .Where(record => record.ApplicantUserId == userId && record.ApplyDate.Year == year)
                .OrderByDescending(record => record.OutboundNo)
                .ToListAsync();
        }

        public Task<List<int>> GetExistingApplyYearsAsync()
        {
            return _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Select(record => record.ApplyDate.Year)
                .Distinct()
                .OrderByDescending(year => year)
                .ToListAsync();
        }

        public async Task<List<string>> GetOutboundNosByPrefixAsync(string prefix)
        {
            return await _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Where(record => record.OutboundNo.StartsWith(prefix))
                .Select(record => record.OutboundNo)
                .ToListAsync();
        }

        public async Task<int> SaveOrUpdateRecordGraphAsync(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            if (record.Id == 0)
            {
                _dbContext.YearlyArchiveOutboundRecords.Add(record);
            }
            else
            {
                var existing = await _dbContext.YearlyArchiveOutboundRecords
                    .Include(r => r.Items)
                    .Include(r => r.SyncEntries)
                    .FirstOrDefaultAsync(r => r.Id == record.Id);

                if (existing == null)
                {
                    _dbContext.YearlyArchiveOutboundRecords.Add(record);
                }
                else
                {
                    _dbContext.Entry(existing).CurrentValues.SetValues(record);

                    var incomingItemIds = record.Items.Where(item => item.Id > 0).Select(item => item.Id).ToHashSet();
                    var persistedItemIds = await _dbContext.YearlyArchiveOutboundItems
                        .Where(item => item.OutboundRecordId == existing.Id && item.Id > 0)
                        .Select(item => item.Id)
                        .ToListAsync();

                    foreach (int removedId in persistedItemIds.Where(id => !incomingItemIds.Contains(id)))
                    {
                        var removed = existing.Items.FirstOrDefault(item => item.Id == removedId)
                            ?? await _dbContext.YearlyArchiveOutboundItems.FindAsync(removedId);
                        if (removed == null)
                        {
                            continue;
                        }

                        existing.Items.Remove(removed);
                        _dbContext.YearlyArchiveOutboundItems.Remove(removed);
                    }

                    foreach (var item in record.Items)
                    {
                        if (item.Id == 0)
                        {
                            item.OutboundRecordId = existing.Id;
                            if (!existing.Items.Contains(item))
                            {
                                existing.Items.Add(item);
                            }
                        }
                        else
                        {
                            var tracked = existing.Items.FirstOrDefault(i => i.Id == item.Id);
                            if (tracked != null)
                            {
                                _dbContext.Entry(tracked).CurrentValues.SetValues(item);
                            }
                        }
                    }

                    MergeSyncEntries(existing, record);

                    await _dbContext.SaveChangesAsync();
                    return existing.Id;
                }
            }

            await _dbContext.SaveChangesAsync();
            return record.Id;
        }

        private void MergeSyncEntries(YearlyArchiveOutboundRecord existing, YearlyArchiveOutboundRecord incoming)
        {
            var incomingSyncEntryIds = incoming.SyncEntries
                .Where(entry => entry.Id > 0)
                .Select(entry => entry.Id)
                .ToHashSet();

            foreach (var removed in existing.SyncEntries
                         .Where(entry => entry.Id > 0 && !incomingSyncEntryIds.Contains(entry.Id))
                         .ToList())
            {
                existing.SyncEntries.Remove(removed);
                _dbContext.YearlyArchiveOutboundSyncEntries.Remove(removed);
            }

            foreach (var entry in incoming.SyncEntries)
            {
                if (entry.Id == 0)
                {
                    entry.OutboundRecordId = existing.Id;
                    if (!existing.SyncEntries.Contains(entry))
                    {
                        existing.SyncEntries.Add(entry);
                    }
                }
                else
                {
                    var tracked = existing.SyncEntries.FirstOrDefault(existingEntry => existingEntry.Id == entry.Id);
                    if (tracked != null)
                    {
                        tracked.OutboundRecordId = existing.Id;
                        tracked.OutboundItemId = entry.OutboundItemId;
                        tracked.FilingFactId = entry.FilingFactId;
                        tracked.EntryKind = entry.EntryKind;
                        tracked.Phase = entry.Phase;
                        tracked.OperatedBy = entry.OperatedBy;
                        tracked.Remark = entry.Remark;
                        tracked.CreatedAt = entry.CreatedAt;
                        tracked.UpdatedAt = entry.UpdatedAt;
                    }
                }
            }
        }

        public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();

        public Task<YearlyArchiveFilingFact?> GetFilingFactByIdAsync(int filingFactId)
        {
            return _dbContext.YearlyArchiveFilingFacts
                .AsNoTracking()
                .FirstOrDefaultAsync(fact => fact.Id == filingFactId);
        }

        public async Task<Dictionary<int, YearlyArchiveFilingFact>> GetFilingFactsByIdsForUpdateAsync(IReadOnlyCollection<int> filingFactIds)
        {
            if (filingFactIds.Count == 0)
            {
                return new Dictionary<int, YearlyArchiveFilingFact>();
            }

            var facts = await _dbContext.YearlyArchiveFilingFacts
                .Where(fact => filingFactIds.Contains(fact.Id))
                .ToListAsync();

            return facts.ToDictionary(fact => fact.Id);
        }

        public async Task<Dictionary<int, YearlyArchiveRegisterMedia>> GetRegisterMediasByIdsForUpdateAsync(IReadOnlyCollection<int> registerMediaIds)
        {
            if (registerMediaIds.Count == 0)
            {
                return new Dictionary<int, YearlyArchiveRegisterMedia>();
            }

            var medias = await _dbContext.YearlyArchiveRegisterMedias
                .Where(media => registerMediaIds.Contains(media.Id))
                .ToListAsync();

            return medias.ToDictionary(media => media.Id);
        }

        public Task<string?> GetRegisterMediaTypeAsync(int registerMediaId)
        {
            return _dbContext.YearlyArchiveRegisterMedias
                .AsNoTracking()
                .Where(media => media.Id == registerMediaId)
                .Select(media => media.MediaType)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetRegisterMediaStockCopyCountAsync(int registerMediaId)
        {
            int mediaCount = await _dbContext.YearlyArchiveRegisterMedias
                .AsNoTracking()
                .Where(media => media.Id == registerMediaId)
                .Select(media => media.MediaCount)
                .FirstOrDefaultAsync();

            return Math.Max(1, mediaCount);
        }

        public async Task<IReadOnlyList<ActiveWithdrawalReservationSnapshot>> GetActiveWithdrawalReservationsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds,
            int? excludeOutboundRecordId)
        {
            if (filingFactIds.Count == 0)
            {
                return Array.Empty<ActiveWithdrawalReservationSnapshot>();
            }

            int[] inFlightStatuses =
            [
                YearlyArchiveOutboundRecord.Submitted,
                YearlyArchiveOutboundRecord.Approved,
                YearlyArchiveOutboundRecord.SignedUploaded
            ];

            var query =
                from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
                join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                    on item.OutboundRecordId equals record.Id
                where filingFactIds.Contains(item.FilingFactId)
                    && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && item.ReservationStatus == ArchiveOutboundDomainValues.SyncEntryPhaseActive
                    && inFlightStatuses.Contains(record.Status)
                select new { item, record };

            if (excludeOutboundRecordId is int excludeId && excludeId > 0)
            {
                query = query.Where(row => row.record.Id != excludeId);
            }

            var rows = await query.ToListAsync();

            return rows
                .Select(row => new ActiveWithdrawalReservationSnapshot
                {
                    FilingFactId = row.item.FilingFactId,
                    OutboundRecordId = row.record.Id,
                    OutboundNo = row.record.OutboundNo,
                    ReservedCopyCount = Math.Max(1, row.item.CopyCount ?? 1)
                })
                .ToList();
        }

        public async Task<IReadOnlyDictionary<int, int>> GetCompletedOutstandingWithdrawalCopyCountsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            var rows = await (
                from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
                join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                    on item.OutboundRecordId equals record.Id
                where filingFactIds.Contains(item.FilingFactId)
                    && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && item.NeedReturn
                    && record.Status == YearlyArchiveOutboundRecord.Completed
                    && item.ReservationStatus == ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed
                select new { item.FilingFactId, item.CopyCount })
                .ToListAsync();

            return rows
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(row => Math.Max(1, row.CopyCount ?? 1)));
        }

        public async Task<IReadOnlyDictionary<int, SimulatedFilingFactCopyCountSnapshot>> GetSimulatedFilingFactCopyCountSnapshotsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return new Dictionary<int, SimulatedFilingFactCopyCountSnapshot>();
            }

            var factIdList = filingFactIds.Distinct().ToList();

            var outboundRows = await (
                from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
                join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                    on item.OutboundRecordId equals record.Id
                where factIdList.Contains(item.FilingFactId)
                    && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && record.Status == YearlyArchiveOutboundRecord.Completed
                select new
                {
                    item.FilingFactId,
                    item.NeedReturn,
                    item.ReservationStatus,
                    item.CopyCount,
                }).ToListAsync();

            var pendingReturnByFactId = outboundRows
                .Where(row => row.NeedReturn
                    && string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed, StringComparison.Ordinal))
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(row => Math.Max(1, row.CopyCount ?? 1)));

            var noReturnByFactId = outboundRows
                .Where(row => !row.NeedReturn
                    && !string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseReturned, StringComparison.Ordinal))
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(row => Math.Max(1, row.CopyCount ?? 1)));

            var returnRows = await (
                from returnItem in _dbContext.YearlyArchiveReturnItems.AsNoTracking()
                join returnRecord in _dbContext.YearlyArchiveReturnRecords.AsNoTracking()
                    on returnItem.ReturnRecordId equals returnRecord.Id
                where factIdList.Contains(returnItem.FilingFactId)
                    && returnRecord.Status == YearlyArchiveReturnRecord.Completed
                select new
                {
                    returnItem.FilingFactId,
                    returnItem.LossCopyCount,
                    returnItem.ReturnCopyCount,
                    returnItem.ItemCondition,
                }).ToListAsync();

            var lostByFactId = returnRows
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Sum(row => row.LossCopyCount > 0
                        ? Math.Max(0, row.LossCopyCount)
                        : ArchiveReturnDomainValues.IsLossCondition(row.ItemCondition)
                            ? Math.Max(1, row.ReturnCopyCount)
                            : 0));

            return factIdList.ToDictionary(
                factId => factId,
                factId => new SimulatedFilingFactCopyCountSnapshot
                {
                    PendingReturnCopyCount = pendingReturnByFactId.GetValueOrDefault(factId),
                    NoReturnCopyCount = noReturnByFactId.GetValueOrDefault(factId),
                    LostCopyCount = lostByFactId.GetValueOrDefault(factId),
                });
        }

        public Task<List<YearlyArchiveOutboundSyncEntry>> GetActiveSyncEntriesByRecordIdAsync(int recordId)
        {
            return _dbContext.YearlyArchiveOutboundSyncEntries
                .Where(entry => entry.OutboundRecordId == recordId
                    && entry.Phase != ArchiveOutboundDomainValues.SyncEntryPhaseCancelled
                    && entry.Phase != ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed)
                .ToListAsync();
        }

        public Task<List<SystemAttachment>> GetAttachmentsByBusinessIdAsync(int businessId)
        {
            return _dbContext.SystemAttachments
                .Where(attachment => attachment.BusinessId == businessId
                    && attachment.BusinessType == ArchiveOutboundDomainValues.BusinessTypeAttachment)
                .OrderByDescending(attachment => attachment.UploadTime)
                .ToListAsync();
        }

        public Task<List<SystemAttachment>> GetOrphanAttachmentsByBusinessNoAsync(string businessNo, string businessType)
        {
            return _dbContext.SystemAttachments
                .Where(attachment => attachment.BusinessNo == businessNo
                    && attachment.BusinessType == businessType
                    && attachment.BusinessId == 0)
                .ToListAsync();
        }

        public void AddAttachment(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            _dbContext.SystemAttachments.Add(attachment);
        }

        public void RemoveAttachment(SystemAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            _dbContext.SystemAttachments.Remove(attachment);
        }

        public Task<SystemAttachment?> GetAttachmentByIdAsync(int attachmentId)
        {
            return _dbContext.SystemAttachments.FindAsync(attachmentId).AsTask();
        }

        public async Task LinkOrphanAttachmentsToRecordAsync(string businessNo, string businessType, int recordId)
        {
            var orphans = await _dbContext.SystemAttachments
                .Where(attachment => attachment.BusinessNo == businessNo
                    && attachment.BusinessType == businessType
                    && attachment.BusinessId == 0)
                .ToListAsync();

            foreach (var attachment in orphans)
            {
                attachment.BusinessId = recordId;
            }

            if (orphans.Count > 0)
            {
                await _dbContext.SaveChangesAsync();
            }
        }

        public Task<List<YearlyArchiveOutboundRecord>> GetSubmittedRecordsPastDeadlineAsync(DateTime asOf)
        {
            return _dbContext.YearlyArchiveOutboundRecords
                .Where(record => record.Status == YearlyArchiveOutboundRecord.Submitted
                    && record.ApprovalDeadline != null
                    && record.ApprovalDeadline < asOf)
                .ToListAsync();
        }

        public Task<List<YearlyArchiveOutboundRecord>> GetPendingRecordsForToDoAsync(int takeCount)
        {
            return _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Where(record => record.Status == YearlyArchiveOutboundRecord.Submitted
                    || record.Status == YearlyArchiveOutboundRecord.Approved
                    || record.Status == YearlyArchiveOutboundRecord.SignedUploaded)
                .OrderByDescending(record => record.SubmittedAt ?? record.ApplyDate)
                .Take(takeCount)
                .ToListAsync();
        }

        public async Task<List<OpticalDiscMedium>> GetOpticalDiscMediaByElectronicUnitIdForUpdateAsync(int unitId)
        {
            if (unitId <= 0)
            {
                return new List<OpticalDiscMedium>();
            }

            var links = await _dbContext.YearlyElectronicArchiveUnitDiscLinks
                .Include(link => link.OpticalDiscMedium!)
                    .ThenInclude(disc => disc.Ledger)
                .Include(link => link.OpticalDiscMedium!)
                    .ThenInclude(disc => disc.Transactions)
                .Where(link => link.YearlyElectronicArchiveUnitId == unitId)
                .ToListAsync();

            return links
                .Select(link => link.OpticalDiscMedium)
                .Where(disc => disc != null && !disc.IsDeleted)
                .Cast<OpticalDiscMedium>()
                .ToList();
        }

        public Task<OpticalDiscMedium?> GetOpticalDiscMediumByCodeForUpdateAsync(string discCode)
        {
            string normalized = discCode.Trim();
            return _dbContext.OpticalDiscMedia
                .Include(disc => disc.Ledger)
                .Include(disc => disc.Transactions)
                .FirstOrDefaultAsync(disc => disc.DiscCode == normalized && !disc.IsDeleted);
        }

        public async Task<List<HardDiskMedium>> GetHardDiskMediaByElectronicUnitIdForUpdateAsync(int unitId)
        {
            if (unitId <= 0)
            {
                return new List<HardDiskMedium>();
            }

            var links = await _dbContext.YearlyElectronicArchiveUnitMediumLinks
                .Include(link => link.HardDiskMedium!)
                    .ThenInclude(medium => medium.Ledger)
                .Where(link => link.YearlyElectronicArchiveUnitId == unitId)
                .ToListAsync();

            return links
                .Select(link => link.HardDiskMedium)
                .Where(medium => medium != null && !medium.IsDeleted)
                .Cast<HardDiskMedium>()
                .ToList();
        }

        public Task<HardDiskMedium?> GetHardDiskMediumByCodeForUpdateAsync(string diskCode)
        {
            string normalized = diskCode.Trim();
            return _dbContext.HardDiskMedia
                .Include(medium => medium.Ledger)
                .FirstOrDefaultAsync(medium => medium.DiskCode == normalized && !medium.IsDeleted);
        }

        public Task<YearlyElectronicArchiveUnit?> GetElectronicArchiveUnitByIdForUpdateAsync(int unitId)
        {
            if (unitId <= 0)
            {
                return Task.FromResult<YearlyElectronicArchiveUnit?>(null);
            }

            return _dbContext.YearlyElectronicArchiveUnits
                .FirstOrDefaultAsync(unit => unit.Id == unitId);
        }

        public Task<List<YearlyArchiveFilingFact>> GetInArchiveFilingFactsByContainerAsync(string mediaKind, string containerCode)
        {
            string normalizedCode = containerCode.Trim();
            return _dbContext.YearlyArchiveFilingFacts
                .AsNoTracking()
                .Where(fact => fact.MediaKind == mediaKind
                    && fact.ContainerCode == normalizedCode
                    && fact.LifecycleStatus == FilingFactLifecycleStatus.InArchive)
                .OrderBy(fact => fact.FilingFactNo)
                .ToListAsync();
        }

        public async Task<decimal> GetUsedDataSizeMbByHardDiskCodeAsync(string diskCode)
        {
            if (string.IsNullOrWhiteSpace(diskCode))
            {
                return 0m;
            }

            string normalizedCode = diskCode.Trim();
            var dataSizeValues = await _dbContext.YearlyElectronicArchiveUnitMediaItemLinks
                .AsNoTracking()
                .Where(link => link.MediumCode == normalizedCode)
                .Select(link => link.DataSizeMb)
                .ToListAsync();

            return dataSizeValues.Sum();
        }

        public Task<YearlyArchiveBox?> GetYearlyArchiveBoxByIdForUpdateAsync(int boxId)
        {
            if (boxId <= 0)
            {
                return Task.FromResult<YearlyArchiveBox?>(null);
            }

            return _dbContext.YearlyArchiveBoxes
                .FirstOrDefaultAsync(box => box.Id == boxId);
        }

        public Task<YearlyArchiveBox?> GetYearlyArchiveBoxByIdAsync(int boxId)
        {
            if (boxId <= 0)
            {
                return Task.FromResult<YearlyArchiveBox?>(null);
            }

            return _dbContext.YearlyArchiveBoxes
                .AsNoTracking()
                .FirstOrDefaultAsync(box => box.Id == boxId);
        }

        public async Task<List<YearlyArchiveBox>> ListInUseSimulatedArchiveBoxesAsync(string? projectName, string? year)
        {
            var query = _dbContext.YearlyArchiveBoxes
                .AsNoTracking()
                .Where(box => box.ContainerLifecycleStatus == ArchiveContainerLifecycleStatus.InUse
                    && !string.IsNullOrWhiteSpace(box.BoxLocationCode));

            string normalizedProject = projectName?.Trim() ?? string.Empty;
            string normalizedYear = year?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedProject))
            {
                query = query.Where(box => box.ProjectName == normalizedProject);
            }

            if (!string.IsNullOrWhiteSpace(normalizedYear))
            {
                query = query.Where(box => box.Year == normalizedYear);
            }

            return await query
                .OrderBy(box => box.Year)
                .ThenBy(box => box.ProjectName)
                .ThenBy(box => box.ArchiveSequenceNo)
                .ToListAsync();
        }

        public void AddYearlyArchiveBox(YearlyArchiveBox box)
        {
            ArgumentNullException.ThrowIfNull(box);
            _dbContext.YearlyArchiveBoxes.Add(box);
        }

        public async Task<List<YearlyArchiveOutboundItem>> GetPendingReturnSimulatedOutboundItemsByBoxIdAsync(int boxId)
        {
            if (boxId <= 0)
            {
                return [];
            }

            var factIds = await _dbContext.YearlyArchiveFilingFacts
                .AsNoTracking()
                .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                    && fact.ContainerId == boxId
                    && fact.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated)
                .Select(fact => fact.Id)
                .ToListAsync();

            return await GetPendingReturnSimulatedOutboundItemsByFilingFactIdsAsync(factIds);
        }

        public async Task<List<YearlyArchiveOutboundItem>> GetPendingReturnSimulatedOutboundItemsByFilingFactIdsAsync(
            IReadOnlyCollection<int> filingFactIds)
        {
            if (filingFactIds == null || filingFactIds.Count == 0)
            {
                return [];
            }

            var ids = filingFactIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            return await (
                from item in _dbContext.YearlyArchiveOutboundItems
                join record in _dbContext.YearlyArchiveOutboundRecords
                    on item.OutboundRecordId equals record.Id
                where ids.Contains(item.FilingFactId)
                    && item.MediaKind == ArchiveRegisterDomainValues.MediaKindSimulated
                    && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && item.NeedReturn
                    && record.Status == YearlyArchiveOutboundRecord.Completed
                    && item.ReservationStatus == ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed
                orderby record.Id, item.SortOrder, item.Id
                select item)
                .ToListAsync();
        }

        public async Task<List<YearlyArchiveBoxMediaItemRow>> GetYearlyArchiveBoxMediaItemRowsForSyncAsync(YearlyArchiveBox box)
        {
            ArgumentNullException.ThrowIfNull(box);

            string normalizedBoxCode = box.BoxLocationCode?.Trim() ?? string.Empty;
            var facts = await _dbContext.YearlyArchiveFilingFacts
                .Where(fact => fact.ContainerKind == ArchiveContainerKind.ArchiveBox
                    && (fact.ContainerId == box.Id
                        || (!string.IsNullOrWhiteSpace(normalizedBoxCode)
                            && (fact.BoxLocationCode == normalizedBoxCode
                                || fact.CurrentStorageLocation == normalizedBoxCode
                                || fact.StorageLocation == normalizedBoxCode))))
                .OrderBy(fact => fact.FormNo)
                .ThenBy(fact => fact.ItemName)
                .ThenBy(fact => fact.Id)
                .ToListAsync();

            if (facts.Count == 0)
            {
                facts = await BuildSyntheticFactsFromMediaItemLinksForSyncAsync(box);
            }

            if (facts.Count == 0)
            {
                return [];
            }

            var factIds = facts.Select(fact => fact.Id).Where(id => id > 0).ToList();
            var outboundRows = factIds.Count == 0
                ? []
                : (
                    from item in _dbContext.YearlyArchiveOutboundItems.AsNoTracking()
                    join record in _dbContext.YearlyArchiveOutboundRecords.AsNoTracking()
                        on item.OutboundRecordId equals record.Id
                    where factIds.Contains(item.FilingFactId)
                        && item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                        && record.Status == YearlyArchiveOutboundRecord.Completed
                    select new
                    {
                        item.FilingFactId,
                        item.NeedReturn,
                        item.ReservationStatus,
                        item.CopyCount,
                    }).ToList()
                    .Select(row => new
                    {
                        row.FilingFactId,
                        row.NeedReturn,
                        row.ReservationStatus,
                        CopyCount = Math.Max(1, row.CopyCount ?? 1),
                    })
                    .ToList();

            var pendingReturnByFactId = outboundRows
                .Where(row => row.NeedReturn
                    && string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed, StringComparison.Ordinal))
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.CopyCount));

            var noReturnByFactId = outboundRows
                .Where(row => !row.NeedReturn
                    && !string.Equals(row.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseReturned, StringComparison.Ordinal))
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(row => row.CopyCount));

            var returnRows = factIds.Count == 0
                ? []
                : (
                    from returnItem in _dbContext.YearlyArchiveReturnItems.AsNoTracking()
                    join returnRecord in _dbContext.YearlyArchiveReturnRecords.AsNoTracking()
                        on returnItem.ReturnRecordId equals returnRecord.Id
                    where factIds.Contains(returnItem.FilingFactId)
                        && returnRecord.Status == YearlyArchiveReturnRecord.Completed
                    select new
                    {
                        returnItem.FilingFactId,
                        returnItem.ItemCondition,
                        returnItem.ReturnCopyCount,
                    }).ToList();

            var lostByFactId = returnRows
                .Where(row => ArchiveReturnDomainValues.IsLossCondition(row.ItemCondition))
                .GroupBy(row => row.FilingFactId)
                .ToDictionary(group => group.Key, group => group.Sum(row => Math.Max(1, row.ReturnCopyCount)));

            return facts.Select(fact => new YearlyArchiveBoxMediaItemRow
            {
                Fact = fact,
                PendingReturnCopyCount = fact.Id > 0 ? pendingReturnByFactId.GetValueOrDefault(fact.Id) : 0,
                NoReturnCopyCount = fact.Id > 0 ? noReturnByFactId.GetValueOrDefault(fact.Id) : 0,
                LostCopyCount = fact.Id > 0 ? lostByFactId.GetValueOrDefault(fact.Id) : 0,
            }).ToList();
        }

        public void RemoveArchiveBoxPlacementByBoxCode(string boxCode)
        {
            if (string.IsNullOrWhiteSpace(boxCode))
            {
                return;
            }

            string normalized = boxCode.Trim();
            var placement = _dbContext.CabinetArchiveBoxPlacements
                .FirstOrDefault(item => item.BoxCode == normalized);
            if (placement != null)
            {
                _dbContext.CabinetArchiveBoxPlacements.Remove(placement);
            }
        }

        private async Task<List<YearlyArchiveFilingFact>> BuildSyntheticFactsFromMediaItemLinksForSyncAsync(YearlyArchiveBox box)
        {
            var links = await _dbContext.YearlyArchiveBoxMediaItemLinks
                .Where(link => link.YearlyArchiveBoxId == box.Id)
                .Include(link => link.MediaItem)
                    .ThenInclude(item => item.MediaEntry)
                        .ThenInclude(media => media!.RegisterRecord)
                .ToListAsync();

            if (links.Count == 0)
            {
                return [];
            }

            return links
                .Where(link => link.MediaItem != null)
                .OrderBy(link => link.MediaItem!.MediaEntry?.RegisterRecord?.FormNo ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.MediaItem!.ContentDesc ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(link => link.Id)
                .Select(link =>
                {
                    var mediaItem = link.MediaItem!;
                    var record = mediaItem.MediaEntry?.RegisterRecord;
                    var mediaEntry = mediaItem.MediaEntry;
                    return new YearlyArchiveFilingFact
                    {
                        MediaKind = ArchiveRegisterDomainValues.MediaKindSimulated,
                        RegisterRecordId = record?.Id ?? 0,
                        RegisterMediaId = mediaEntry?.Id ?? mediaItem.YearlyArchiveRegisterMediaId,
                        MediaItemId = mediaItem.Id,
                        FormNo = record?.FormNo?.Trim() ?? string.Empty,
                        MaterialName = record?.MaterialName?.Trim() ?? box.ProjectName?.Trim() ?? string.Empty,
                        ProjectName = record?.ProjectName?.Trim() ?? box.ProjectName?.Trim() ?? string.Empty,
                        ItemType = mediaItem.ItemType?.Trim() ?? string.Empty,
                        ItemName = mediaItem.ContentDesc?.Trim() ?? string.Empty,
                        ContentCount = mediaItem.ContentCount,
                        ContainerKind = ArchiveContainerKind.ArchiveBox,
                        ContainerId = box.Id,
                        ContainerCode = box.ArchiveSequenceNo?.Trim() ?? string.Empty,
                        StorageLocation = box.BoxLocationCode?.Trim() ?? string.Empty,
                        BoxLocationCode = box.BoxLocationCode?.Trim() ?? string.Empty,
                        StorageCarrierType = mediaEntry?.MediaType?.Trim() ?? string.Empty,
                        FiledAt = box.ArchivedDate,
                        FiledBy = box.ArchivedBy?.Trim() ?? string.Empty,
                        LifecycleStatus = FilingFactLifecycleStatus.InArchive,
                    };
                })
                .ToList();
        }
    }
}
