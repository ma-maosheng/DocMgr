using System;
using System.Collections.Generic;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Models.Inventory
{
    /// <summary>
    /// 库管资料总览一次只打开的资料类型。历史盒按台账拆成地形图、航片、其他。
    /// </summary>
    public static class StockContainerBrowseKind
    {
        public const string YearlyBox = StockContainerKind.YearlyBox;
        public const string ElectronicBag = StockContainerKind.ElectronicBag;
        public const string HistoryTopo = "HistoryTopo";
        public const string HistoryAerial = "HistoryAerial";
        public const string HistoryOther = "HistoryOther";
        public const string HistoryMisc = StockContainerKind.HistoryMisc;

        public static string MapDisplay(string? kind) => kind switch
        {
            YearlyBox => "年度盒",
            ElectronicBag => "电子袋",
            HistoryTopo => "历史地形图",
            HistoryAerial => "历史航片",
            HistoryOther => "历史其他",
            HistoryMisc => "历史杂项",
            _ => StockContainerKind.MapDisplay(kind)
        };

        /// <summary>浏览类型对应的容器查询种类。</summary>
        public static string ToContainerKind(string? browseKind) => browseKind switch
        {
            ElectronicBag => StockContainerKind.ElectronicBag,
            HistoryTopo or HistoryAerial or HistoryOther => StockContainerKind.HistoryBox,
            HistoryMisc => StockContainerKind.HistoryMisc,
            _ => StockContainerKind.YearlyBox
        };

        /// <summary>历史盒浏览类型对应的台账类别；非历史盒返回 null。</summary>
        public static string? ToHistoryMaterialKind(string? browseKind) => browseKind switch
        {
            HistoryTopo => HistoryArchiveDisposalDomainValues.MaterialKindTopoMap,
            HistoryAerial => HistoryArchiveDisposalDomainValues.MaterialKindAerialPhoto,
            HistoryOther => HistoryArchiveDisposalDomainValues.MaterialKindOtherMap,
            _ => null
        };

        /// <summary>年度盒与电子袋才有档案年度。</summary>
        public static bool UsesArchiveYear(string? browseKind) =>
            string.Equals(browseKind, YearlyBox, StringComparison.Ordinal)
            || string.Equals(browseKind, ElectronicBag, StringComparison.Ordinal);
    }

    /// <summary>
    /// 库管资料总览的容器类型（年度盒 / 电子袋 / 历史盒 / 历史杂项）。
    /// </summary>
    public static class StockContainerKind
    {
        public const string YearlyBox = "YearlyBox";
        public const string ElectronicBag = "ElectronicBag";
        public const string HistoryBox = "HistoryBox";
        public const string HistoryMisc = "HistoryMisc";

        public static string MapDisplay(string? kind) => kind switch
        {
            YearlyBox => "年度档案盒",
            ElectronicBag => "电子介质袋",
            HistoryBox => "历史存档盒",
            HistoryMisc => "历史杂项",
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

        /// <summary>筛选：在柜容器（年度在用/借出中、历史在库/离库锁定等，不含已退库）。</summary>
        public const string FilterActive = "Active";

        /// <summary>筛选：仅容器内仍有借出资料的在柜行。</summary>
        public const string FilterHasBorrowed = "HasBorrowed";

        /// <summary>筛选：已退库容器（年度已清空/销号/迁出/处置，历史已离库）。</summary>
        public const string FilterInactive = "Inactive";

        /// <summary>筛选：全部容器（含已退库）。</summary>
        public const string FilterAll = "All";
    }

    /// <summary>
    /// 库管资料总览容器筛选条件。
    /// </summary>
    public sealed class StockContainerSearchCriteria
    {
        /// <summary>空或空集合表示四类全选。</summary>
        public IReadOnlyList<string> ContainerKinds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 历史盒台账类别（<see cref="HistoryArchiveDisposalDomainValues.MaterialKindTopoMap"/> 等）。
        /// 空表示不按类别裁剪。
        /// </summary>
        public string? HistoryMaterialKind { get; set; }

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
        /// 生命周期筛选：
        /// <see cref="StockContainerLifecycleDisplay.FilterActive"/> /
        /// <see cref="StockContainerLifecycleDisplay.FilterHasBorrowed"/> /
        /// <see cref="StockContainerLifecycleDisplay.FilterInactive"/> /
        /// <see cref="StockContainerLifecycleDisplay.FilterAll"/>。
        /// </summary>
        public string LifecycleFilter { get; set; } = StockContainerLifecycleDisplay.FilterActive;

        /// <summary>
        /// 查询是否装载已退库容器。由状态筛选推导：仅「已退库」或「全部」为 true。
        /// </summary>
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

        /// <summary>杂项资料大类。</summary>
        public string MajorCategory { get; init; } = string.Empty;

        /// <summary>杂项资料子类。</summary>
        public string SubCategory { get; init; } = string.Empty;

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

        /// <summary>比例尺（地形图 / 航片）。</summary>
        public string Scale { get; init; } = string.Empty;

        /// <summary>图号或序号。</summary>
        public string CodeText { get; init; } = string.Empty;

        /// <summary>成图日期、航摄日期或年度区间。</summary>
        public string DateText { get; init; } = string.Empty;

        /// <summary>区域、测区或资料分类。</summary>
        public string AreaText { get; init; } = string.Empty;

        /// <summary>台账或明细备注。</summary>
        public string Remark { get; init; } = string.Empty;

        /// <summary>电子资料数据量（MB），无数据时为空。</summary>
        public string DataSizeDisplay { get; init; } = string.Empty;

        /// <summary>存储载体类型。</summary>
        public string CarrierText { get; init; } = string.Empty;

        /// <summary>电子立档存储路径。</summary>
        public string PathText { get; init; } = string.Empty;

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
        public const string HistoryMiscMaterial = "HistoryMiscMaterial";
        public const string HistoryMiscMaterialLine = "HistoryMiscMaterialLine";
    }
}
