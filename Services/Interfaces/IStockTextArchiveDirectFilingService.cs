using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocMgr.Models.Projects;
using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 存档文本资料直办立档：以档案盒为单位手工登记模拟介质，由资料室资料管理员直接完成建档与立档。
    /// </summary>
    public interface IStockTextArchiveDirectFilingService
    {
        /// <summary>
        /// 匹配库内同年同名项目。
        /// </summary>
        ProjectInfo? FindProject(string year, string projectName);

        /// <summary>
        /// 列出已登记实施年度并集（项目信息 ∪ 模拟盒 ∪ 电子袋），供开放域下拉。
        /// </summary>
        IReadOnlyList<string> ListRegisteredYears();

        /// <summary>
        /// 列出指定年度下已登记项目名称并集，供开放域下拉。
        /// </summary>
        IReadOnlyList<string> ListRegisteredProjectNames(string year);

        /// <summary>
        /// 列出指定实施年度的已有项目（并集；含仅出现在立档容器中的名称）。
        /// </summary>
        IReadOnlyList<ProjectInfo> ListProjectsByYear(string year);

        /// <summary>
        /// 列出年度资料专用档口候选（按盒规格过滤容量）。
        /// </summary>
        Task<IReadOnlyList<ArchiveBoxTargetLocationOption>> GetBoxSlotOptionsAsync(
            string projectName,
            string year,
            string boxSpecification);

        /// <summary>
        /// 推荐年度资料专用档口。
        /// </summary>
        Task<ArchiveBoxLocationSuggestion?> SuggestBoxSlotAsync(
            string projectName,
            string year,
            string boxSpecification);

        /// <summary>
        /// 预览下一条模拟盒号（不占用编号）。年度须为项目实施年度。
        /// </summary>
        Task<string> PeekNextArchiveSequenceNoAsync(string year);

        /// <summary>
        /// 确认立档前的完整性与逻辑核验。
        /// </summary>
        Task<IReadOnlyList<string>> CollectCommitErrorsAsync(
            StockTextArchiveDirectFilingRequest request,
            User? currentUser);

        /// <summary>
        /// 资料室资料管理员一次完成建档单合成与模拟盒立档。
        /// </summary>
        Task<StockTextArchiveDirectFilingResult> CommitAsync(
            StockTextArchiveDirectFilingRequest request,
            User? currentUser);

        /// <summary>
        /// 列出 Excel 工作表名称，供导入前选择。
        /// </summary>
        IReadOnlyList<string> ListExcelSheetNames(string filePath);

        /// <summary>
        /// 解析指定工作表，按档案盒编号分组。
        /// </summary>
        StockTextArchiveExcelParseResult ParseExcel(string filePath, string sheetName);

        /// <summary>
        /// 校验 Excel 解析出的各盒（档口用途、规格、占用、建档字段）。
        /// </summary>
        Task<IReadOnlyList<StockTextArchiveExcelBoxValidation>> ValidateExcelImportAsync(
            IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes,
            User? currentUser,
            IProgress<(int Current, int Total, string Status)>? progress = null);

        /// <summary>
        /// 按盒循环提交；失败盒不阻断后续盒。
        /// </summary>
        Task<StockTextArchiveExcelImportCommitResult> CommitExcelImportAsync(
            IReadOnlyList<StockTextArchiveExcelBoxDraft> boxes,
            User? currentUser,
            IProgress<(int Current, int Total, string Status)>? progress = null);
    }
}
