using DocMgr.Models.Inventory;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>
    /// 库管资料总览：容器主行查询与内容明细读取。
    /// </summary>
    public interface IStockContainerOverviewRepository
    {
        Task<IReadOnlyList<int>> GetDistinctYearsAsync();

        Task<IReadOnlyList<StockContainerOverviewRow>> SearchContainersAsync(
            StockContainerSearchCriteria criteria);

        Task<IReadOnlyList<StockContainerContentItem>> GetContentsAsync(
            string containerKind,
            int containerId);

        /// <summary>
        /// 按登记介质明细 ID 读取电子目录/文件条目（只读）。
        /// </summary>
        Task<IReadOnlyList<YearlyArchiveRegisterElectronicMediaItemEntry>> GetElectronicContentEntriesAsync(
            int mediaItemId);
    }
}
