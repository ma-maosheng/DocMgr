namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 电子介质袋级摘要（袋头信息，非资料子项行）。
    /// </summary>
    public sealed class CabinetElectronicArchiveBagHeader
    {
        public string ElectronicArchiveNo { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string StorageCarrierType { get; init; } = string.Empty;

        public string LinkedMediumCodes { get; init; } = string.Empty;

        public string Disposition { get; init; } = string.Empty;

        public string ContentSummary { get; init; } = string.Empty;

        public int MediaCount { get; init; }

        public string ArchivedBy { get; init; } = string.Empty;

        public string ArchivedDateText { get; init; } = string.Empty;

        public string Remarks { get; init; } = string.Empty;
    }
}
