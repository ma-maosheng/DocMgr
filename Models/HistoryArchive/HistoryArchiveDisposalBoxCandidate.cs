namespace DocMgr.Models.HistoryArchive
{
    /// <summary>
    /// 历史存档离库处置候选档案盒。
    /// </summary>
    public sealed class HistoryArchiveDisposalBoxCandidate
    {
        public string BoxCode { get; init; } = string.Empty;

        public string BoxSpecification { get; init; } = string.Empty;

        public string CabinetName { get; init; } = string.Empty;

        public string FaceCode { get; init; } = string.Empty;

        public string SlotCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string ContentSummary { get; init; } = string.Empty;

        public int LedgerRecordCount { get; init; }

        public string SourceRecordKeys { get; init; } = string.Empty;

        public bool IsMixedPlacement { get; init; }

        public IReadOnlyList<string> RelatedBoxCodes { get; init; } = Array.Empty<string>();

        public int RelatedBoxCount => RelatedBoxCodes.Count;

        public string RelatedBoxCodesText =>
            RelatedBoxCodes.Count == 0
                ? string.Empty
                : string.Join("；", RelatedBoxCodes);

        /// <summary>摆放来源为跨类同盒，不可纳入本单。</summary>
        public bool IsCrossTypeMixed { get; init; }

        /// <summary>组内盒已被其他未办结单占用。</summary>
        public bool IsLockedByOther { get; init; }

        public bool IsSelectable => !IsCrossTypeMixed && !IsLockedByOther && LedgerRecordCount > 0;
    }
}
