using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 流转台账：横向查询入库后的出库/归还实物流转及出库流程节点（不含立档业务）。
    /// </summary>
    public interface IArchiveCirculationLedgerService
    {
        Task<IReadOnlyList<MaterialTransactionLedgerRow>> SearchCirculationAsync(CirculationLedgerSearchCriteria criteria);

        Task<IReadOnlyList<CirculationContainerMasterRow>> SearchNeverCirculatedContainersAsync(
            CirculationLedgerSearchCriteria criteria);

        Task<IReadOnlyList<MaterialOutboundProcessNodeSearchRow>> SearchOutboundProcessNodesAsync(
            OutboundProcessNodeLedgerSearchCriteria criteria);
    }
}
