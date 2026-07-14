using DocMgr.Data;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.YearlyArchive
{
    public sealed partial class ArchiveMaterialTransactionRepository : IArchiveMaterialTransactionRepository
    {
        private readonly AppDbContext _dbContext;

        public ArchiveMaterialTransactionRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<YearlyArchiveMaterialTransaction>> GetByFilingFactIdAsync(int filingFactId)
        {
            var items = await _dbContext.YearlyArchiveMaterialTransactions
                .AsNoTracking()
                .Where(item => item.FilingFactId == filingFactId)
                .OrderByDescending(item => item.OperatedAt)
                .ThenByDescending(item => item.Id)
                .ToListAsync();

            return items;
        }

        public async Task<HashSet<string>> GetExistingDedupKeysAsync(IEnumerable<string> dedupKeys)
        {
            var keys = dedupKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (keys.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var existing = await _dbContext.YearlyArchiveMaterialTransactions
                .AsNoTracking()
                .Where(item => keys.Contains(item.DedupKey))
                .Select(item => item.DedupKey)
                .ToListAsync();

            return existing.ToHashSet(StringComparer.Ordinal);
        }

        public void AddTransactions(IEnumerable<YearlyArchiveMaterialTransaction> transactions)
        {
            ArgumentNullException.ThrowIfNull(transactions);
            _dbContext.YearlyArchiveMaterialTransactions.AddRange(transactions);
        }

        public Task<YearlyArchiveFilingFact?> GetFilingFactAsync(int filingFactId)
        {
            return _dbContext.YearlyArchiveFilingFacts
                .AsNoTracking()
                .FirstOrDefaultAsync(fact => fact.Id == filingFactId);
        }

        public async Task<IReadOnlyList<(YearlyArchiveRelocationItem Item, YearlyArchiveRelocationRecord Record)>> GetRelocationEventsAsync(int filingFactId)
        {
            var items = await _dbContext.YearlyArchiveRelocationItems
                .AsNoTracking()
                .Where(item => item.FilingFactId == filingFactId)
                .Include(item => item.RelocationRecord)
                .OrderByDescending(item => item.RelocationRecord.OperatedAt)
                .ThenByDescending(item => item.Id)
                .ToListAsync();

            return items
                .Select(item => (item, item.RelocationRecord))
                .ToList();
        }

        public async Task<IReadOnlyList<(YearlyArchiveOutboundSyncEntry Entry, YearlyArchiveOutboundRecord Record, YearlyArchiveOutboundItem Item)>> GetOutboundSyncEventsAsync(int filingFactId)
        {
            var entries = await _dbContext.YearlyArchiveOutboundSyncEntries
                .AsNoTracking()
                .Where(entry => entry.FilingFactId == filingFactId)
                .Include(entry => entry.OutboundRecord)
                .OrderByDescending(entry => entry.CreatedAt)
                .ThenByDescending(entry => entry.Id)
                .ToListAsync();

            if (entries.Count == 0)
            {
                return Array.Empty<(YearlyArchiveOutboundSyncEntry, YearlyArchiveOutboundRecord, YearlyArchiveOutboundItem)>();
            }

            var itemIds = entries.Select(entry => entry.OutboundItemId).Distinct().ToList();
            var items = await _dbContext.YearlyArchiveOutboundItems
                .AsNoTracking()
                .Where(item => itemIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id);

            return entries
                .Where(entry => items.ContainsKey(entry.OutboundItemId))
                .Select(entry => (entry, entry.OutboundRecord, items[entry.OutboundItemId]))
                .ToList();
        }

        public async Task<IReadOnlyList<(YearlyArchiveReturnItem Item, YearlyArchiveReturnRecord Record)>> GetReturnEventsAsync(int filingFactId)
        {
            var items = await _dbContext.YearlyArchiveReturnItems
                .AsNoTracking()
                .Where(item => item.FilingFactId == filingFactId)
                .Include(item => item.ReturnRecord)
                .ToListAsync();

            return items
                .Where(item => item.ReturnRecord.Status == YearlyArchiveReturnRecord.Completed)
                .OrderByDescending(item => item.ReturnRecord.CompletedAt ?? item.ReturnRecord.UpdatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => (item, item.ReturnRecord))
                .ToList();
        }

        public Task SaveChangesAsync() => _dbContext.SaveChangesAsync();
    }
}
