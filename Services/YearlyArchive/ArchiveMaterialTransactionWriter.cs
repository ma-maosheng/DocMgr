using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 年度资料流转履历写入器。
    /// </summary>
    public sealed class ArchiveMaterialTransactionWriter : IArchiveMaterialTransactionWriter
    {
        private readonly IArchiveMaterialTransactionRepository _repository;

        public ArchiveMaterialTransactionWriter(IArchiveMaterialTransactionRepository repository)
        {
            _repository = repository;
        }

        public async Task AppendFilingTransactionsAsync(IReadOnlyList<YearlyArchiveFilingFact> facts)
        {
            ArgumentNullException.ThrowIfNull(facts);

            var candidates = facts
                .Where(fact => fact.Id > 0 || (fact.SourceLinkId > 0 && !string.IsNullOrWhiteSpace(fact.SourceLinkType)))
                .Select(ArchiveMaterialTransactionSupport.BuildFilingTransaction)
                .ToList();

            await AppendIfNotExistsAsync(candidates);
        }

        public async Task AppendRelocationTransactionsAsync(YearlyArchiveRelocationRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var candidates = record.Items
                .Where(item => item.FilingFactId > 0)
                .Select(item => ArchiveMaterialTransactionSupport.BuildRelocationTransaction(record, item))
                .ToList();

            await AppendIfNotExistsAsync(candidates);
        }

        public async Task AppendOutboundCompletionTransactionsAsync(YearlyArchiveOutboundRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);

            var candidates = ArchiveMaterialTransactionSupport
                .BuildOutboundCompletionTransactions(record)
                .ToList();

            await AppendIfNotExistsAsync(candidates);
        }

        public async Task AppendReturnCompletionTransactionsAsync(
            YearlyArchiveReturnRecord returnRecord,
            YearlyArchiveOutboundRecord outboundRecord,
            IReadOnlyDictionary<int, string>? afterLifecycleByFactId = null)
        {
            ArgumentNullException.ThrowIfNull(returnRecord);
            ArgumentNullException.ThrowIfNull(outboundRecord);

            var candidates = ArchiveMaterialTransactionSupport
                .BuildReturnCompletionTransactions(returnRecord, outboundRecord, afterLifecycleByFactId)
                .ToList();

            await AppendIfNotExistsAsync(candidates);
        }

        private async Task AppendIfNotExistsAsync(IReadOnlyList<YearlyArchiveMaterialTransaction> candidates)
        {
            if (candidates.Count == 0)
            {
                return;
            }

            var dedupKeys = candidates.Select(item => item.DedupKey).ToList();
            var existing = await _repository.GetExistingDedupKeysAsync(dedupKeys);
            var toAdd = candidates
                .Where(item => !existing.Contains(item.DedupKey))
                .ToList();

            if (toAdd.Count == 0)
            {
                return;
            }

            _repository.AddTransactions(toAdd);
        }
    }
}
