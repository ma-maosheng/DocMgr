using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// Cursor 版立档测试（独立于模拟登记页原有自动立档逻辑）。
    /// </summary>
    public interface IArchiveFilingCursorTestService
    {
        /// <summary>
        /// 对模拟登记中已办结、待立档的数据执行立档测试并返回清单。
        /// </summary>
        Task<ArchiveFilingAutomationResult> RunCursorFilingTestAsync(User? operatorUser);
    }
}
