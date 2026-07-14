namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveRegisterAttachmentFlowResult(bool Success, string Message, SystemAttachment? Attachment = null)
    {
        public static ArchiveRegisterAttachmentFlowResult Ok(string message, SystemAttachment attachment) => new(true, message, attachment);
        public static ArchiveRegisterAttachmentFlowResult Ok(string message) => new(true, message, null);
        public static ArchiveRegisterAttachmentFlowResult Fail(string message) => new(false, message, null);
    }
}
