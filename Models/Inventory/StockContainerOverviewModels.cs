using System;
using System.Collections.Generic;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Inventory
{
    /// <summary>
    /// 库管资料总览的容器类型（年度盒 / 电子袋 / 历史盒）。
    /// </summary>
    public static class StockContainerKind
    {
        public const string YearlyBox = "YearlyBox";
        public const string ElectronicBag = "ElectronicBag";
        public const string HistoryBox = "HistoryBox";

        public static string MapDisplay(string? kind) => kind switch
        {
            YearlyBox => "年度档案盒",
            ElectronicBag => "电子介质袋",
            HistoryBox => "历史存档盒",
            _ => string.IsNullOrWhiteSpace(kind) ? "—" : kind
        };
    }

    /// <summary>
    /// 容器主行展示态（投影层，不写回实体）。
    /// </summary>
    public static class StockContainerLifecycleDisplay
    {
        public const string InUse = "在用";
        public const string PartialBorrowed = "部分借出";
        public const string Borrowed = "借出中";
        public const string Emptied = "已清空";
        public const string Retired = "已销号";
        public const string Relocated = "已迁出";
        public const string Disposed = "已处置";
        public const string HistoryInStock = "在库";
        public const string HistoryLocked = "离库锁定";
        public const string HistoryDisposed = "已离库";

        /// <summary>筛选：在柜可用（含部分借出/借出中）。</summary>
        public const string FilterActive = "Active";

        /// <summary>筛选：含借出内容。</summary>
        public const string FilterHasBorrowed = "HasBorrowed";

        /// <summary>筛选：已停用/已离库。</summary>
        public const string FilterInactive = "Inactive";
    }

    /// <summary>
    /// 库管资料总览容器筛选条件。
    /// </summary>
    public sealed class StockContainerSearchCriteria
    {
        /// <summary>空或空集合表示三类全选。</summary>
        public IReadOnlyList<string> ContainerKinds { get; set; } = Array.Empty<string>();

        public string? Year { get; set; }

        public string Keyword { get; set; } = string.Empty;

        /// <summary>为 true 时关键词按原文区分大小写；默认 false（忽略大小写）。</summary>
        public bool KeywordCaseSensitive { get; set; }

        /// <summary>
        /// 为 true 时关键词额外匹配电子介质目录/文件明细的名称（<c>EntryName</c>）；默认 false。
        /// </summary>
        public bool IncludeElectronicEntryNames { get; set; }

        public string StorageLocationKeyword { get; set; } = string.Empty;

        /// <summary>
        /// 生命周期筛选：空=全部；
        /// <see cref="StockContainerLifecycleDisplay.FilterActive"/> /
        /// <see cref="StockContainerLifecycleDisplay.FilterHasBorrowed"/> /
        /// <see cref="StockContainerLifecycleDisplay.FilterInactive"/>。
        /// </summary>
        public string LifecycleFilter { get; set; } = string.Empty;

        /// <summary>为 false 时默认排除已清空/销号/迁出/处置及历史已离库（除非筛选显式为已停用）。</summary>
        public bool IncludeInactive { get; set; }

        public DateTime? ArchivedFrom { get; set; }

        public DateTime? ArchivedTo { get; set; }
    }

    /// <summary>
    /// 库管资料总览容器主行。
    /// </summary>
    public sealed class StockContainerOverviewRow
    {
        public string ContainerKind { get; init; } = string.Empty;

        public string ContainerKindDisplay => StockContainerKind.MapDisplay(ContainerKind);

        public int ContainerId { get; init; }

        public string ContainerCode { get; init; } = string.Empty;

        public string ProjectOrCategory { get; init; } = string.Empty;

        public string Year { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string LifecycleStatusDisplay { get; init; } = string.Empty;

        public int ItemCount { get; init; }

        public string ContentSummary { get; init; } = string.Empty;

        public string Specs { get; init; } = string.Empty;

        public string PlacementMode { get; init; } = string.Empty;

        public string PlacementModeDisplay =>
            string.Equals(PlacementMode, "FrontOut", StringComparison.OrdinalIgnoreCase)
                ? "盒面向外"
                : string.IsNullOrWhiteSpace(PlacementMode)
                    ? string.Empty
                    : "盒脊向外";

        public string ArchivedBy { get; init; } = string.Empty;

        public DateTime ArchivedDate { get; init; }

        public string ArchivedDateDisplay =>
            ArchivedDate == default ? string.Empty : ArchivedDate.ToString("yyyy-MM-dd");

        public string Remarks { get; init; } = string.Empty;

        public bool HasBorrowedContent { get; init; }

        public bool IsInactive { get; init; }
    }

    /// <summary>
    /// 库管资料总览容器内容明细。
    /// </summary>
    public sealed class StockContainerContentItem
    {
        public string ContainerKind { get; init; } = string.Empty;

        public int ContainerId { get; init; }

        public string SourceEntity { get; init; } = string.Empty;

        public int SourceId { get; init; }

        public string MaterialKindDisplay { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ConfidentialLevel { get; init; } = string.Empty;

        public int ContentCount { get; init; }

        public string LifecycleStatus { get; init; } = string.Empty;

        public string LifecycleStatusDisplay { get; init; } = string.Empty;

        public string ArchiveCopyRoleDisplay { get; init; } = string.Empty;

        public string MediumCode { get; init; } = string.Empty;

        public string FormNo { get; init; } = string.Empty;

        public string ExtraInfo { get; init; } = string.Empty;

        /// <summary>
        /// 立档事实关联的登记介质明细 ID；非电子立档事实为 0。
        /// </summary>
        public int MediaItemId { get; init; }

        /// <summary>是否为电子介质立档行（可查看目录/文件明细）。</summary>
        public bool IsElectronicMedia =>
            string.Equals(
                MaterialKindDisplay,
                ArchiveRegisterDomainValues.MediaKindElectronic,
                StringComparison.Ordinal)
            && MediaItemId > 0;
    }

    /// <summary>
    /// 内容明细来源实体名。
    /// </summary>
    public static class StockContainerContentSourceEntity
    {
        public const string FilingFact = "YearlyArchiveFilingFact";
        public const string TopoMap = "TopoMap";
        public const string AerialPhoto = "AerialPhoto";
        public const string OtherMap = "OtherMap";
    }
}
