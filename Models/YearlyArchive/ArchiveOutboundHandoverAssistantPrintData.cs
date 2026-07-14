namespace DocMgr.Models.YearlyArchive
{
    public sealed class ArchiveOutboundHandoverAssistantPrintLine
    {
        public string Category { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public bool IsChecked { get; init; }
    }

    public sealed class ArchiveOutboundHandoverAssistantPrintData
    {
        public string OutboundNo { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ApplicantDept { get; init; } = string.Empty;

        public string MaterialSummary { get; init; } = string.Empty;

        public IReadOnlyList<ArchiveOutboundHandoverAssistantPrintLine> Lines { get; init; } =
            Array.Empty<ArchiveOutboundHandoverAssistantPrintLine>();
    }
}
