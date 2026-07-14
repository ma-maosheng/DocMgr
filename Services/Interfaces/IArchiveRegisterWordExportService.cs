using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 年度资料入档申请审批单 Word 导出。
    /// </summary>
    public interface IArchiveRegisterWordExportService
    {
        void ExportToFile(ArchiveRegisterPrintData data, string filePath);
    }
}
