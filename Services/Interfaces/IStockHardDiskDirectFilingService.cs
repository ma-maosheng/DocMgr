using System.Collections.Generic;
using System.Threading.Tasks;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.YearlyArchive;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 存量硬盘直办立档：读取硬盘参数与四级目录后，由资料室资料管理员直接完成硬盘登记与电子立档。
    /// </summary>
    public interface IStockHardDiskDirectFilingService
    {
        /// <summary>
        /// 扫描硬盘根目录四级结构。
        /// </summary>
        StockHardDiskDirectoryScanResult ScanDirectory(string? rootPath);

        /// <summary>
        /// 匹配库内同年同名项目。
        /// </summary>
        ProjectInfo? FindProject(string year, string projectName);

        /// <summary>
        /// 列出指定实施年度的已有项目，供直办立档核对名称。
        /// </summary>
        IReadOnlyList<ProjectInfo> ListProjectsByYear(string year);

        /// <summary>
        /// 查询同年度/项目已有电子硬盘袋数量。
        /// </summary>
        Task<int> CountExistingHardDiskBagsAsync(string projectName, string year);

        /// <summary>
        /// 按序列号查找已登记硬盘。
        /// </summary>
        Task<HardDiskMedium?> FindMediumBySerialNumberAsync(string? serialNumber);

        /// <summary>
        /// 推荐年度数据硬盘专用档口完整位置。
        /// </summary>
        Task<string?> RecommendDataSlotLocationAsync();

        /// <summary>
        /// 列出年度数据硬盘专用档口候选。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDataSlotOptionsAsync();

        /// <summary>
        /// 将档口键规范为带档内序号的数据盘位置。
        /// </summary>
        Task<string> ResolveDataFullLocationAsync(string? requestedLocation);

        /// <summary>
        /// 预览下一条电子袋号（不占用编号）。
        /// 存量直办须传入目录扫描得到的项目实施年度，勿用系统当前年。
        /// </summary>
        Task<string> PeekNextElectronicArchiveNoAsync(string year);

        /// <summary>
        /// 确认立档前的完整性与逻辑核验。
        /// </summary>
        Task<IReadOnlyList<string>> CollectCommitErrorsAsync(StockHardDiskDirectFilingRequest request, User? currentUser);

        /// <summary>
        /// 资料室资料管理员一次完成硬盘登记与电子立档。
        /// </summary>
        Task<StockHardDiskDirectFilingResult> CommitAsync(StockHardDiskDirectFilingRequest request, User? currentUser);
    }
}
