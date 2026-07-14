namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveRegisterFlowResult(bool Success, string Message)
    {
        public static ArchiveRegisterFlowResult Ok(string message) => new(true, message);
        public static ArchiveRegisterFlowResult Fail(string message) => new(false, message);
    }
}
