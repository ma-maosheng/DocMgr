using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 年度资料流转履历仓储。
    /// </summary>
    public interface IArchiveMaterialTransactionRepository
    {
        Task<IReadOnlyList<YearlyArchiveMaterialTransaction>> GetByFilingFactIdAsync(int filingFactId);

        Task<HashSet<string>> GetExistingDedupKeysAsync(IEnumerable<string> dedupKeys);

        void AddTransactions(IEnumerable<YearlyArchiveMaterialTransaction> transactions);

        Task<YearlyArchiveFilingFact?> GetFilingFactAsync(int filingFactId);

        Task<IReadOnlyList<(YearlyArchiveRelocationItem Item, YearlyArchiveRelocationRecord Record)>> GetRelocationEventsAsync(int filingFactId);

        Task<IReadOnlyList<(YearlyArchiveOutboundSyncEntry Entry, YearlyArchiveOutboundRecord Record, YearlyArchiveOutboundItem Item)>> GetOutboundSyncEventsAsync(int filingFactId);

        Task<IReadOnlyList<(YearlyArchiveReturnItem Item, YearlyArchiveReturnRecord Record)>> GetReturnEventsAsync(int filingFactId);

        Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchRelocationLedgerAsync(RelocationLedgerSearchCriteria criteria);

        Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchCirculationLedgerAsync(CirculationLedgerSearchCriteria criteria);

        Task<IReadOnlyList<CirculationContainerMasterRow>> SearchNeverCirculatedContainersAsync(
            CirculationLedgerSearchCriteria criteria);

        Task<IReadOnlyList<MaterialOutboundProcessNodeSearchRow>> SearchOutboundProcessNodeLedgerAsync(
            OutboundProcessNodeLedgerSearchCriteria criteria);

        Task SaveChangesAsync();
    }
}
