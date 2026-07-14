using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.Interfaces
{
    /// <summary>
    /// 资料出库申请审批单 Word 导出。
    /// </summary>
    public interface IArchiveOutboundWordExportService
    {
        void ExportToFile(ArchiveOutboundPrintData data, string filePath);
    }
}
