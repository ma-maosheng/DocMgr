using DocMgr.Models.ArchiveContainers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 年度资料立档服务契约：档案盒与电子介质袋的创建、归并及位置建议。
    /// </summary>
    public interface IArchiveFilingService
    {
        /// <summary>
        /// 获取尚未立档的登记记录（状态=1 或 0/1 depending on logic, usually 1=Submitted）
        /// </summary>
        Task<List<YearlyArchiveRegisterRecord>> GetPendingRecordsAsync(string? year = null);

        /// <summary>
        /// 获取待模拟介质立档的登记记录。
        /// </summary>
        Task<List<YearlyArchiveRegisterRecord>> GetPendingSimulatedRecordsAsync(string? year = null);

        /// <summary>
        /// 获取待电子介质立档的登记记录。
        /// </summary>
        Task<List<YearlyArchiveRegisterRecord>> GetPendingElectronicRecordsAsync(string? year = null);

        /// <summary>
        /// 统计指定年度内已办结且模拟介质轨已全部立档的登记单数量。
        /// </summary>
        Task<int> GetFiledSimulatedRecordCountAsync(string? year = null);

        /// <summary>
        /// 统计指定年度内已办结且电子介质轨已全部立档的登记单数量。
        /// </summary>
        Task<int> GetFiledElectronicRecordCountAsync(string? year = null);

        /// <summary>
        /// 根据项目名称和年度，获取已存在的档案盒列表（用于判定是否首次立档、追加归档）
        /// </summary>
        Task<List<YearlyArchiveBox>> GetExistingBoxesForProjectAsync(string projectName, string year);

        /// <summary>
        /// 根据项目名称和年度，获取已存在的电子立档单元列表。
        /// </summary>
        Task<List<YearlyElectronicArchiveUnit>> GetExistingElectronicUnitsForProjectAsync(string projectName, string year);

        /// <summary>
        /// 生成下一个年度档案编号 (例如: 2026-001)
        /// </summary>
        Task<string> GenerateNextArchiveSequenceNoAsync(string year);

        /// <summary>
        /// 生成下一个电子立档编号。
        /// </summary>
        Task<string> GenerateNextElectronicArchiveNoAsync(string year);

        /// <summary>
        /// 获取指定格子里当前已有多少盒（用于展示占用数，不用于生成档内序号）。
        /// </summary>
        Task<int> GetBoxCountInCellAsync(string cabinetName, string side, int row, int col);

        /// <summary>
        /// 获取指定模拟介质档口内可用的最小盒序号（从 1 起填补空位）。
        /// </summary>
        Task<int> GetMinimumAvailableBoxSequenceInCellAsync(string cabinetName, string side, int row, int col, int? excludeBoxId = null);

        /// <summary>
        /// 获取指定电子介质档口内当前已存放的介质袋数量。
        /// </summary>
        Task<int> GetElectronicUnitCountInCellAsync(string cabinetName, string side, int row, int col);

        /// <summary>
        /// 获取指定电子介质档口内可用的最小序号（从 1 起填补空位）。
        /// </summary>
        Task<int> GetMinimumAvailableElectronicSequenceInCellAsync(string cabinetName, string side, int row, int col, int? excludeUnitId = null);

        /// <summary>
        /// 执行立档：创建档案盒并归入选中的资料子项。
        /// </summary>
        /// <param name="newBox">档案盒信息</param>
        /// <param name="mediaItemIds">关联的资料子项ID列表</param>
        Task CreateArchiveBoxAsync(YearlyArchiveBox newBox, List<int> mediaItemIds);

        /// <summary>
        /// 执行电子介质立档：创建电子立档单元并归入选中的电子介质条目。
        /// </summary>
        /// <param name="newUnit">电子立档单元信息</param>
        /// <param name="mediaEntryIds">关联的电子介质条目ID列表</param>
        Task CreateElectronicArchiveUnitAsync(YearlyElectronicArchiveUnit newUnit, List<int> mediaEntryIds);

        /// <summary>
        /// 按提交请求创建新的电子介质袋并执行立档校验。
        /// </summary>
        Task<ElectronicArchiveSubmissionResult> SubmitNewElectronicArchiveUnitAsync(ElectronicArchiveSubmissionRequest request, User? currentUser);

        /// <summary>
        /// 将资料子项并入既有档案盒。
        /// </summary>
        /// <param name="boxId">档案盒ID</param>
        /// <param name="mediaItemIds">关联的资料子项ID列表</param>
        Task AppendToArchiveBoxAsync(int boxId, List<int> mediaItemIds);

        /// <summary>
        /// 根据当前项目、年度和档案盒规格，列出全部符合要求的可选档口。
        /// </summary>
        Task<IReadOnlyList<ArchiveBoxTargetLocationOption>> GetArchiveBoxTargetLocationOptionsAsync(
            string projectName,
            string year,
            string boxSpecification);

        /// <summary>
        /// 根据当前项目、年度和档案盒规格，给出建议档口位置。
        /// </summary>
        Task<ArchiveBoxLocationSuggestion?> SuggestArchiveBoxLocationAsync(string projectName, string year, string boxSpecification);

        /// <summary>
        /// 将选中的电子介质条目并入既有电子立档单元。
        /// </summary>
        /// <param name="unitId">电子立档单元ID</param>
        /// <param name="updatedUnit">并入时用于更新既有电子立档单元的字段</param>
        /// <param name="mediaEntryIds">关联的电子介质条目ID列表</param>
        Task AppendToElectronicArchiveUnitAsync(int unitId, YearlyElectronicArchiveUnit updatedUnit, List<int> mediaEntryIds);

        /// <summary>
        /// 按提交请求将电子介质条目并入既有电子介质袋并执行立档校验。
        /// </summary>
        Task<ElectronicArchiveSubmissionResult> SubmitAppendElectronicArchiveUnitAsync(ElectronicArchiveSubmissionRequest request, User? currentUser);

        /// <summary>
        /// 预览新建电子介质袋立档拟执行逻辑：执行与提交相同的校验，仅返回拟变更报告，不写入数据库。
        /// </summary>
        Task<ElectronicArchiveSubmissionResult> PreviewNewElectronicArchiveUnitAsync(ElectronicArchiveSubmissionRequest request, User? currentUser);

        /// <summary>
        /// 预览并入既有电子介质袋立档拟执行逻辑：执行与提交相同的校验，仅返回拟变更报告，不写入数据库。
        /// </summary>
        Task<ElectronicArchiveSubmissionResult> PreviewAppendElectronicArchiveUnitAsync(ElectronicArchiveSubmissionRequest request, User? currentUser);

        /// <summary>
        /// 是否存在同名同年的档案盒（防止重复创建意外）
        /// </summary>
        Task<bool> IsArchiveSequenceExistsAsync(string sequenceNo);

        /// <summary>
        /// 是否存在重复的电子立档编号。
        /// </summary>
        Task<bool> IsElectronicArchiveNoExistsAsync(string sequenceNo);

        /// <summary>
        /// 查询指定硬盘介质当前关联的电子立档单元信息。
        /// </summary>
        /// <param name="mediumIds">硬盘介质主键集合。</param>
        Task<IReadOnlyList<HardDiskElectronicArchiveLinkInfo>> GetElectronicArchiveLinkInfosAsync(IEnumerable<int> mediumIds);

        Task DeleteRecordAsync(int id); // [新增]

        /// <summary>
        /// 根据项目和年度获取统一容器摘要（档案盒/电子介质袋）。
        /// </summary>
        Task<List<ArchiveContainerSummary>> GetExistingContainerSummariesForProjectAsync(string projectName, string year, ArchiveContainerKind containerKind);

        /// <summary>
        /// 解析电子介质立档界面决策。
        /// </summary>
        ElectronicArchiveUiDecision ResolveElectronicArchiveUiDecision(ElectronicArchiveScenarioInput input);

        /// <summary>
        /// 解析硬盘带回场景的目标硬盘选择模式。
        /// </summary>
        string ResolveHardDiskSelectionMode(string? hardDiskCopyTargetMode);
    }
}