namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质业务流程结果。
    /// </summary>
    public sealed record HardDiskMediaFlowResult(bool Success, string Message)
    {
        /// <summary>
        /// 创建成功结果。
        /// </summary>
        public static HardDiskMediaFlowResult Ok(string message) => new(true, message);

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        public static HardDiskMediaFlowResult Fail(string message) => new(false, message);
    }
}
