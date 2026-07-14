namespace DocMgr.Models.YearlyArchive
{
    public sealed record ArchiveOutboundFlowResult(bool Success, string Message)
    {
        public static ArchiveOutboundFlowResult Ok(string message) => new(true, message);
        public static ArchiveOutboundFlowResult Fail(string message) => new(false, message);
    }
}
