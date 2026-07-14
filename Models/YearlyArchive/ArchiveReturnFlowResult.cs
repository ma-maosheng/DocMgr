namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还业务流程结果。
    /// </summary>
    public sealed record ArchiveReturnFlowResult(bool Success, string Message, int RecordId = 0)
    {
        public static ArchiveReturnFlowResult Ok(string message, int recordId = 0) => new(true, message, recordId);
        public static ArchiveReturnFlowResult Fail(string message) => new(false, message);
    }
}
