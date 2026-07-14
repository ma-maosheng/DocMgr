using DocMgr.Models.SystemSettings;

namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveOutboundAttachmentFlowResult(bool Success, string Message, SystemAttachment? Attachment = null)
    {
        public static ArchiveOutboundAttachmentFlowResult Ok(string message, SystemAttachment attachment) => new(true, message, attachment);

        public static ArchiveOutboundAttachmentFlowResult Ok(string message) => new(true, message, null);

        public static ArchiveOutboundAttachmentFlowResult Fail(string message) => new(false, message, null);
    }
}
