using DocMgr.Services.Interfaces;

namespace DocMgr.Services.Shared
{
    /// <summary>
    /// Excel 导入读取循环的进度上报节流。
    /// </summary>
    internal static class ExcelImportProgressSupport
    {
        public static void ReportReadRow(IOperationProgressSession? progress, int current, int total)
        {
            if (progress == null || total <= 0)
            {
                return;
            }

            if (current == 1 || current == total || current % 50 == 0)
            {
                progress.Report(current, total, $"正在读取第 {current} / {total} 行…");
            }
        }
    }
}
