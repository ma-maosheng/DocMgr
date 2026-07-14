namespace DocMgr.ViewModels.YearlyArchive
{
    /// <summary>
    /// 电子介质立档第一步中「电子介质表单」下拉项（对应一条登记介质明细）。
    /// </summary>
    public sealed class ElectronicMediaFormListItem
    {
        public int MediaEntryId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string MediaType { get; init; } = string.Empty;

        public string Disposition { get; init; } = string.Empty;

        /// <summary>
        /// 介质编号：与登记资料一致；有有效编号视同资料室借出硬盘，无编号或占位「—」视同外来硬盘（立档时据此推断留存来源）。
        /// </summary>
        public string MediumCode { get; init; } = string.Empty;

        /// <summary>
        /// 下拉框显示文案。
        /// </summary>
        public string DisplayLabel { get; init; } = string.Empty;

        /// <summary>
        /// 当前条目的已立档份数。
        /// </summary>
        public int ArchivedCount { get; init; }

        /// <summary>
        /// 当前条目的总份数。
        /// </summary>
        public int TotalCount { get; init; }

        /// <summary>
        /// 立档进度状态：未启动立档 / 已部分立档 / 已全部立档。
        /// </summary>
        public string FilingStatus { get; init; } = "未启动立档";

        /// <summary>
        /// 立档进度摘要。
        /// </summary>
        public string FilingProgressText => $"{ArchivedCount}/{TotalCount}";

        /// <summary>
        /// 是否已全部立档完成。
        /// </summary>
        public bool IsFullyFiled => ArchivedCount >= TotalCount;

        /// <summary>
        /// 是否可作为当前立档介质。
        /// </summary>
        public bool CanSelectAsCurrent => !IsFullyFiled;

        /// <summary>
        /// 表单列显示文案。
        /// </summary>
        public string FormDisplayText => $"{FormNo} | {MediaType} | {MaterialName}";
    }
}
