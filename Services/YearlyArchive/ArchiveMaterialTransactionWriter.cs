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

            var eligibleFacts = facts
                .Where(fact => fact.Id > 0 || (fact.SourceLinkId > 0 && !string.IsNullOrWhiteSpace(fact.SourceLinkType)))
                .ToList();
            if (eligibleFacts.Count == 0)
            {
                return;
            }

            var registerRecordIds = eligibleFacts
                .Select(fact => fact.RegisterRecordId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            IReadOnlyDictionary<int, NetworkOutboundFilingOrigin> outboundOrigins = registerRecordIds.Count == 0
                ? new Dictionary<int, NetworkOutboundFilingOrigin>()
                : await _repository.GetOutboundFilingOriginsByRegisterRecordIdsAsync(registerRecordIds);

            var candidates = eligibleFacts
                .Select(fact =>
                {
                    if (fact.RegisterRecordId > 0
                        && outboundOrigins.TryGetValue(fact.RegisterRecordId, out NetworkOutboundFilingOrigin? origin)
                        && origin.OutboundRecordId > 0)
                    {
                        return ArchiveMaterialTransactionSupport.BuildNetworkOutboundFilingTransaction(fact, origin);
                    }

                    return ArchiveMaterialTransactionSupport.BuildFilingTransaction(fact);
                })
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
