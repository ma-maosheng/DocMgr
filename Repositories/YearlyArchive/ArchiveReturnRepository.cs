using DocMgr.Data;
using DocMgr.Models.Shared;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed class ArchiveReturnRepository : IArchiveReturnRepository
    {
        private sealed class ArchiveReturnRepositoryTransaction : IArchiveFilingRepositoryTransaction
        {
            private readonly IDbContextTransaction _transaction;

            public ArchiveReturnRepositoryTransaction(IDbContextTransaction transaction)
            {
                _transaction = transaction;
            }

            public Task CommitAsync() => _transaction.CommitAsync();

            public Task RollbackAsync() => _transaction.RollbackAsync();

            public async ValueTask DisposeAsync() => await _transaction.DisposeAsync();
        }

        private readonly AppDbContext _dbContext;

        public ArchiveReturnRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IArchiveFilingRepositoryTransaction> BeginTransactionAsync()
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync();
            return new ArchiveReturnRepositoryTransaction(transaction);
        }

        public async Task<List<string>> GetReturnNosByPrefixAsync(string prefix)
        {
            return await _dbContext.YearlyArchiveReturnRecords
                .AsNoTracking()
                .Where(record => record.ReturnNo.StartsWith(prefix))
                .Select(record => record.ReturnNo)
                .ToListAsync();
        }

        public Task<List<YearlyArchiveReturnRecord>> ListByYearAsync(int year)
        {
            return _dbContext.YearlyArchiveReturnRecords
                .AsNoTracking()
                .Include(record => record.Items)
                .Where(record => record.ReturnDate.Year == year)
                .OrderByDescending(record => record.ReturnNo)
                .ToListAsync();
        }

        public Task<YearlyArchiveReturnRecord?> GetByIdWithDetailsAsync(int id)
        {
            return _dbContext.YearlyArchiveReturnRecords
                .Include(record => record.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
                .FirstOrDefaultAsync(record => record.Id == id);
        }

        public async Task<int> SaveOrUpdateRecordGraphAsync(YearlyArchiveReturnRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var existing = await FindExistingRecordGraphAsync(record);

            if (existing == null)
            {
                _dbContext.YearlyArchiveReturnRecords.Add(record);
                await _dbContext.SaveChangesAsync();
                return record.Id;
            }

            _dbContext.Entry(existing).CurrentValues.SetValues(record);

            var incomingItemIds = record.Items.Where(item => item.Id > 0).Select(item => item.Id).ToHashSet();
            foreach (var removed in existing.Items
                         .Where(item => item.Id > 0 && !incomingItemIds.Contains(item.Id))
                         .ToList())
            {
                existing.Items.Remove(removed);
                _dbContext.YearlyArchiveReturnItems.Remove(removed);
            }

            foreach (var item in record.Items)
            {
                if (item.Id == 0)
                {
                    item.ReturnRecordId = existing.Id;
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

            await _dbContext.SaveChangesAsync();
            return existing.Id;
        }

        private Task<YearlyArchiveReturnRecord?> FindExistingRecordGraphAsync(YearlyArchiveReturnRecord record)
        {
            if (record.Id > 0)
            {
                return _dbContext.YearlyArchiveReturnRecords
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.Id == record.Id);
            }

            if (!string.IsNullOrWhiteSpace(record.ReturnNo))
            {
                return _dbContext.YearlyArchiveReturnRecords
                    .Include(r => r.Items)
                    .FirstOrDefaultAsync(r => r.ReturnNo == record.ReturnNo);
            }

            return Task.FromResult<YearlyArchiveReturnRecord?>(null);
        }

        public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();

        public Task<bool> HasActiveReturnForOutboundAsync(int outboundRecordId, int excludeReturnId = 0)
        {
            return _dbContext.YearlyArchiveReturnRecords
                .AnyAsync(record => record.SourceOutboundRecordId == outboundRecordId
                    && record.Id != excludeReturnId
                    && record.Status != YearlyArchiveReturnRecord.Voided);
        }

        public async Task<List<YearlyArchiveOutboundRecord>> GetReturnableOutboundsAsync(int year)
        {
            var activeReturnOutboundIds = await _dbContext.YearlyArchiveReturnRecords
                .AsNoTracking()
                .Where(record => record.Status != YearlyArchiveReturnRecord.Voided)
                .Select(record => record.SourceOutboundRecordId)
                .Distinct()
                .ToListAsync();

            return await _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Include(record => record.Items)
                .Where(record => record.Status == YearlyArchiveOutboundRecord.Completed
                    && record.ApplyDate.Year == year)
                .Where(record => record.Items.Any(item =>
                    item.UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                    && item.NeedReturn
                    && item.ReservationStatus != ArchiveOutboundDomainValues.SyncEntryPhaseReturned))
                .Where(record => !activeReturnOutboundIds.Contains(record.Id))
                .OrderByDescending(record => record.OutboundNo)
                .ToListAsync();
        }

        public async Task<List<YearlyArchiveOutboundRecord>> GetOverdueReturnOutboundsAsync(DateTime asOf, int take)
        {
            var activeReturnOutboundIds = await _dbContext.YearlyArchiveReturnRecords
                .AsNoTracking()
                .Where(record => record.Status != YearlyArchiveReturnRecord.Voided)
                .Select(record => record.SourceOutboundRecordId)
                .Distinct()
                .ToListAsync();

            var borrowedMediumIds = await _dbContext.HardDiskLedgers
                .AsNoTracking()
                .Where(ledger => ledger.NeedReturn)
                .Where(ledger => ledger.MediaStatus == HardDiskMedium.StatusOutTemporary
                                 || ledger.MediaStatus == HardDiskMedium.StatusOutLongTerm)
                .Select(ledger => ledger.MediumId)
                .ToListAsync();

            var borrowedMediumIdSet = borrowedMediumIds.ToHashSet();

            var candidates = await _dbContext.YearlyArchiveOutboundRecords
                .AsNoTracking()
                .Include(record => record.Items)
                .Where(record => record.Status == YearlyArchiveOutboundRecord.Completed)
                .Where(record => !activeReturnOutboundIds.Contains(record.Id))
                .ToListAsync();

            return candidates
                .Where(record =>
                    ArchiveOutboundReturnSupport.HasOverdueWithdrawalItems(record, asOf)
                    || ArchiveOutboundReturnSupport.HasOverdueDiskRequisitionItems(
                        record,
                        asOf,
                        borrowedMediumIdSet))
                .OrderBy(record => record.ExpectedReturnDate)
                .Take(take)
                .ToList();
        }

        public Task<List<YearlyArchiveReturnRecord>> GetPendingReturnRecordsForToDoAsync(int take)
        {
            return _dbContext.YearlyArchiveReturnRecords
                .AsNoTracking()
                .Include(record => record.Items)
                .Where(record =>
                    record.Status == YearlyArchiveReturnRecord.Submitted
                    || record.Status == YearlyArchiveReturnRecord.Approved
                    || record.Status == YearlyArchiveReturnRecord.SignedUploaded)
                .OrderByDescending(record => record.SubmittedAt ?? record.RegisteredAt ?? record.ReturnDate)
                .Take(take)
                .ToListAsync();
        }

        public Task<List<SystemAttachment>> GetAttachmentsByBusinessIdAsync(int businessId)
        {
            return _dbContext.SystemAttachments
                .Where(attachment => attachment.BusinessId == businessId
                    && attachment.BusinessType == ArchiveReturnDomainValues.BusinessTypeAttachment)
                .OrderByDescending(attachment => attachment.UploadTime)
                .ToListAsync();
        }

        public Task<List<SystemAttachment>> GetAttachmentsByBusinessNoAsync(string businessNo, string businessType)
        {
            return _dbContext.SystemAttachments
                .Where(attachment => attachment.BusinessNo == businessNo
                    && attachment.BusinessType == businessType)
                .OrderByDescending(attachment => attachment.UploadTime)
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
    }
}
