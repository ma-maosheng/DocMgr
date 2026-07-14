namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质附件流程结果。
    /// </summary>
    public sealed record HardDiskMediaAttachmentFlowResult(bool Success, string Message, SystemAttachment? Attachment = null)
    {
        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static HardDiskMediaAttachmentFlowResult Ok(string message, SystemAttachment attachment) => new(true, message, attachment);

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static HardDiskMediaAttachmentFlowResult Ok(string message) => new(true, message, null);

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static HardDiskMediaAttachmentFlowResult Fail(string message) => new(false, message, null);
    }
}
