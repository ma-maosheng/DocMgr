using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 流转台账查询（实物流转不含立档业务）。
    /// </summary>
    public sealed class ArchiveCirculationLedgerService : IArchiveCirculationLedgerService
    {
        private readonly IArchiveMaterialTransactionRepository _repository;

        public ArchiveCirculationLedgerService(IArchiveMaterialTransactionRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchCirculationAsync(
            CirculationLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return _repository.SearchCirculationLedgerAsync(criteria);
        }

        public Task<IReadOnlyList<CirculationContainerMasterRow>> SearchNeverCirculatedContainersAsync(
            CirculationLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return _repository.SearchNeverCirculatedContainersAsync(criteria);
        }

        public Task<IReadOnlyList<MaterialOutboundProcessNodeSearchRow>> SearchOutboundProcessNodesAsync(
            OutboundProcessNodeLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return _repository.SearchOutboundProcessNodeLedgerAsync(criteria);
        }
    }
}
