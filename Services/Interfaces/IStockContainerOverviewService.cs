using DocMgr.Models.Inventory;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 库管资料总览：按年度盒、电子袋、历史地形图、历史航片、历史其他或历史杂项查询与导出。
    /// </summary>
    public interface IStockContainerOverviewService
    {
        Task<IReadOnlyList<int>> GetDistinctYearsAsync();

        Task<IReadOnlyList<StockContainerOverviewRow>> SearchContainersAsync(
            StockContainerSearchCriteria criteria);

        Task<IReadOnlyList<StockContainerContentItem>> GetContentsAsync(
            string containerKind,
            int containerId,
            string? historyMaterialKind = null);

        /// <summary>
        /// 读取电子立档行关联的目录/文件明细（供总览弹窗展示）。
        /// </summary>
        Task<IReadOnlyList<ElectronicMediaItemEntryDisplayItem>> GetElectronicContentEntriesAsync(
            int mediaItemId);

        Task ExportAsync(
            string filePath,
            IReadOnlyList<StockContainerOverviewRow> rows,
            bool includeContents,
            string browseKind);
    }
}
