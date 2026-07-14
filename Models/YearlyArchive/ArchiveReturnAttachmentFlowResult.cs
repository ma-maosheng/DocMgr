using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还附件操作结果。
    /// </summary>
    public sealed record ArchiveReturnAttachmentFlowResult(bool Success, string Message, SystemAttachment? Attachment = null)
    {
        public static ArchiveReturnAttachmentFlowResult Ok(string message, SystemAttachment attachment) => new(true, message, attachment);

        public static ArchiveReturnAttachmentFlowResult Ok(string message) => new(true, message, null);

        public static ArchiveReturnAttachmentFlowResult Fail(string message) => new(false, message, null);
    }
}
