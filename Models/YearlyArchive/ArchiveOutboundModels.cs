namespace DocMgr.Models.YearlyArchive
{
    public sealed class CreateOutboundFromPoolRequest
    {
        public int ResultSetId { get; set; }

        public IReadOnlyList<int> ResultSetItemIds { get; set; } = Array.Empty<int>();
    }

    public sealed class SaveOutboundDraftRequest
    {
        public YearlyArchiveOutboundRecord Record { get; set; } = null!;

        public IReadOnlyList<YearlyArchiveOutboundItem> Items { get; set; } = Array.Empty<YearlyArchiveOutboundItem>();
    }

    public sealed class OutboundListCriteria
    {
        public int Year { get; set; }

        public int? StatusFilter { get; set; }

        public bool OnlyMine { get; set; }

        public ArchiveOutboundWorkspaceMode WorkspaceMode { get; set; }
    }
}
