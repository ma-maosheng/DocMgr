namespace DocMgr.ViewModels.Shared
{
    /// <summary>
    /// 打印预览窗口导出配置。
    /// 未指定 <see cref="ExportAsync"/> 时，预览窗口将按 FlowDocument 通用方式导出 Word。
    /// </summary>
    public sealed class PrintPreviewExportOptions
    {
        /// <summary>专用导出逻辑（如登记/出库审批单的精细版式）。为空则使用通用导出。</summary>
        public Func<Task>? ExportAsync { get; init; }

        /// <summary>通用导出时的默认文件名（含或不含 .docx）。</summary>
        public string? DefaultFileName { get; init; }
    }
}
