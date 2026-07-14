namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 打印预览窗口可选导出配置。
    /// </summary>
    public sealed class PrintPreviewExportOptions
    {
        public required Func<Task> ExportAsync { get; init; }
    }
}
