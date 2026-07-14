using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 年度档案盒内资料子项份数查询行（仓储层装配）。
    /// </summary>
    public sealed class YearlyArchiveBoxMediaItemRow
    {
        public YearlyArchiveFilingFact Fact { get; init; } = null!;

        public int PendingReturnCopyCount { get; init; }

        public int NoReturnCopyCount { get; init; }

        public int LostCopyCount { get; init; }

        /// <summary>登记申请所属项目的实施年度。</summary>
        public string ProjectYear { get; init; } = string.Empty;

        /// <summary>登记申请上的归档目的。</summary>
        public string ArchivePurpose { get; init; } = string.Empty;

        /// <summary>登记资料子项扩展属性（备注、存储目录、电子明细等）。</summary>
        public CabinetArchiveBoxMediaItemSupplement Supplement { get; init; } = CabinetArchiveBoxMediaItemSupplement.Empty;
    }
}
