using System.Collections.Generic;
using System.Threading.Tasks;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 存量光盘直办立档：扫描盘上真实目录并指派编目身份后，由资料管理员直接完成光盘登记与电子立档。
    /// </summary>
    public interface IStockOpticalDiscDirectFilingService
    {
        /// <summary>
        /// 扫描光盘根目录，列出整盘与一级业务文件夹。
        /// </summary>
        StockOpticalDiscDirectoryScanResult ScanDirectory(string? rootPath);

        /// <summary>
        /// 匹配库内同年同名项目。
        /// </summary>
        ProjectInfo? FindProject(string year, string projectName);

        /// <summary>
        /// 列出已登记实施年度并集，供开放域下拉。
        /// </summary>
        IReadOnlyList<string> ListRegisteredYears();

        /// <summary>
        /// 列出指定年度下已登记项目名称并集，供开放域下拉。
        /// </summary>
        IReadOnlyList<string> ListRegisteredProjectNames(string year);

        /// <summary>
        /// 列出指定实施年度的已有项目。
        /// </summary>
        IReadOnlyList<ProjectInfo> ListProjectsByYear(string year);

        /// <summary>
        /// 查询同年度/项目已有电子光盘袋数量。
        /// </summary>
        Task<int> CountExistingOpticalDiscBagsAsync(string projectName, string year);

        /// <summary>
        /// 按光盘编号查找已登记光盘。
        /// </summary>
        Task<OpticalDiscMedium?> FindDiscByCodeAsync(string? discCode);

        /// <summary>
        /// 推荐年度数据光盘专用档口完整位置。
        /// </summary>
        Task<string?> RecommendDataSlotLocationAsync();

        /// <summary>
        /// 列出年度数据光盘专用档口候选。
        /// </summary>
        Task<IReadOnlyList<HardDiskMediaReturnTargetLocationOption>> GetDataSlotOptionsAsync();

        /// <summary>
        /// 将档口键规范为带档内序号的光盘位置。
        /// </summary>
        Task<string> ResolveDataFullLocationAsync(string? requestedLocation);

        /// <summary>
        /// 预览下一条电子袋号（不占用编号）。年度取项目实施年度。
        /// </summary>
        Task<string> PeekNextElectronicArchiveNoAsync(string year);

        /// <summary>
        /// 确认立档前的完整性与逻辑核验。
        /// </summary>
        Task<IReadOnlyList<string>> CollectCommitErrorsAsync(StockOpticalDiscDirectFilingRequest request, User? currentUser);

        /// <summary>
        /// 资料管理员一次完成光盘登记与电子立档。
        /// </summary>
        Task<StockOpticalDiscDirectFilingResult> CommitAsync(StockOpticalDiscDirectFilingRequest request, User? currentUser);
    }
}
