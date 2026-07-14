namespace DocMgr.Models.YearlyArchive
{
    /// <summary>异常归还：可选的在用目标档案盒。</summary>
    public sealed class ArchiveReturnRehomeTargetOption
    {
        public int BoxId { get; init; }

        public string ArchiveSequenceNo { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string Specs { get; init; } = string.Empty;

        public string DisplayText { get; init; } = string.Empty;
    }

    /// <summary>异常归还：新建空盒请求。</summary>
    public sealed class ArchiveReturnCreateEmptyBoxRequest
    {
        public string CabinetName { get; set; } = string.Empty;

        public string Side { get; set; } = string.Empty;

        public int Row { get; set; }

        public int Column { get; set; }

        public int BoxIndex { get; set; } = 1;

        public string Specs { get; set; } = string.Empty;

        public string PlacementMode { get; set; } = "竖放";

        public string Year { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;
    }
}
