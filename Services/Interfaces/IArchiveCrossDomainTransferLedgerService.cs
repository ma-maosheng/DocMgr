using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 跨域流转台账：横向查询离线档案域与生产网络域之间的复制流转流水。
    /// </summary>
    public interface IArchiveCrossDomainTransferLedgerService
    {
        Task<IReadOnlyList<CrossDomainTransferLedgerRow>> SearchAsync(CrossDomainTransferLedgerSearchCriteria criteria);

        Task<IReadOnlyList<string>> GetBusinessNoOptionsAsync(int maxCount = 50);

        Task ExportAsync(string filePath, IReadOnlyList<CrossDomainTransferLedgerRow> rows);
    }
}
