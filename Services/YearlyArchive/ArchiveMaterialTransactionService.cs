using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 年度资料流转履历查询：仅读 MaterialTransactions 表。
    /// </summary>
    public sealed class ArchiveMaterialTransactionService : IArchiveMaterialTransactionService
    {
        private readonly IArchiveMaterialTransactionRepository _repository;

        public ArchiveMaterialTransactionService(IArchiveMaterialTransactionRepository repository)
        {
            _repository = repository;
        }

        public async Task<IReadOnlyList<MaterialTransactionTimelineRow>> GetTimelineByFilingFactIdAsync(int filingFactId)
        {
            if (filingFactId <= 0)
            {
                return Array.Empty<MaterialTransactionTimelineRow>();
            }

            var stored = await _repository.GetByFilingFactIdAsync(filingFactId);
            return stored
                .Select(ArchiveMaterialTransactionSupport.MapTimelineRow)
                .ToList();
        }

        public async Task<IReadOnlyList<MaterialOutboundProcessNodeRow>> GetOutboundProcessNodesByFilingFactIdAsync(int filingFactId)
        {
            if (filingFactId <= 0)
            {
                return Array.Empty<MaterialOutboundProcessNodeRow>();
            }

            var events = await _repository.GetOutboundSyncEventsAsync(filingFactId);
            return events
                .Select(tuple => ArchiveOutboundProcessNodeSupport.MapProcessNode(tuple.Entry, tuple.Record, tuple.Item))
                .OrderByDescending(row => row.OperatedAt)
                .ThenByDescending(row => row.OutboundNo, StringComparer.Ordinal)
                .ToList();
        }
    }
}
