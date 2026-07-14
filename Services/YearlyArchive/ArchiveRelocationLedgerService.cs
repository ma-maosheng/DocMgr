using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;
using DocMgr.Services.Interfaces;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 迁档台账查询。
    /// </summary>
    public sealed class ArchiveRelocationLedgerService : IArchiveRelocationLedgerService
    {
        private readonly IArchiveMaterialTransactionRepository _repository;

        public ArchiveRelocationLedgerService(IArchiveMaterialTransactionRepository repository)
        {
            _repository = repository;
        }

        public Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchAsync(RelocationLedgerSearchCriteria criteria)
        {
            ArgumentNullException.ThrowIfNull(criteria);
            return _repository.SearchRelocationLedgerAsync(criteria);
        }
    }
}
