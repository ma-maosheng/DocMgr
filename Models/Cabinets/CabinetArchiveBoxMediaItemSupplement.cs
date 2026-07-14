namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒内容展示所需的登记资料子项扩展属性（审批时确定，立档事实台账未完整冗余的部分）。
    /// </summary>
    public sealed class CabinetArchiveBoxMediaItemSupplement
    {
        public static CabinetArchiveBoxMediaItemSupplement Empty { get; } = new();

        public string Note { get; init; } = string.Empty;

        public string StoragePath { get; init; } = string.Empty;

        /// <summary>登记介质组上的介质类型（如纸质、U盘等）。</summary>
        public string MediaType { get; init; } = string.Empty;

        /// <summary>登记介质组上的处置方式。</summary>
        public string Disposition { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string SubCategory { get; init; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        public decimal DataSizeMb { get; init; }

        public string ContentEntryBreakdownText { get; init; } = string.Empty;
    }
}
