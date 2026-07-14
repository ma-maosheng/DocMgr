using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 迁档台账：横向查询迁档流转流水。
    /// </summary>
    public interface IArchiveRelocationLedgerService
    {
        Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchAsync(RelocationLedgerSearchCriteria criteria);
    }
}
