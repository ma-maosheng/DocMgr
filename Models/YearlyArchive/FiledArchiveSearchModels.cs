using System;
using System.Collections.Generic;
using DocMgr.Models.ArchiveContainers;

namespace DocMgr.Models.YearlyArchive
{
    public sealed class FiledArchiveSearchHit
    {
        public int FilingFactId { get; init; }

        public string FilingFactNo { get; init; } = string.Empty;

        public string MediaKind { get; init; } = string.Empty;

        public int RegisterRecordId { get; init; }

        public int RegisterMediaId { get; init; }

        public int MediaItemId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string ProvideUnit { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ItemType { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ConfidentialLevel { get; init; } = string.Empty;

        public int ContentCount { get; init; }

        /// <summary>资料子项立档份数展示文案（取自立档事实 <see cref="ContentCount"/>）。</summary>
        public string ContentCountDisplay => ContentCount > 0 ? $"{ContentCount} 份" : "0 份";

        public ArchiveContainerKind ContainerKind { get; init; }

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string StorageCarrierType { get; init; } = string.Empty;

        public string MediumCode { get; init; } = string.Empty;

        public string FilingStoragePath { get; init; } = string.Empty;

        public DateTime FiledAt { get; init; }

        public string FiledBy { get; init; } = string.Empty;

        public string LifecycleStatus { get; init; } = FilingFactLifecycleStatus.InArchive;

        public string CurrentContainerCode { get; init; } = string.Empty;

        public string CurrentStorageLocation { get; init; } = string.Empty;

        public string BorrowHintLevel { get; init; } = FilingFactBorrowHintLevel.None;

        public string BorrowHintText { get; init; } = string.Empty;

        public int? PrimaryFilingFactId { get; init; }

        public string ArchiveCopyRole { get; init; } = FilingFactArchiveCopyRole.Original;

        /// <summary>模拟介质当前库内份数（已扣待还/不还/灭失）；电子介质恒为库存展示基数。</summary>
        public int CurrentInArchiveCopyCount { get; init; }

        /// <summary>模拟介质累计灭失份数。</summary>
        public int LostCopyCount { get; init; }

        /// <summary>模拟介质出库待还份数。</summary>
        public int PendingReturnCopyCount { get; init; }

        /// <summary>模拟介质出库不还份数。</summary>
        public int NoReturnCopyCount { get; init; }

        public bool IsBackupCopy => string.Equals(
            ArchiveCopyRole,
            FilingFactArchiveCopyRole.Backup,
            StringComparison.Ordinal);

        public string ArchiveCopyRoleDisplay => IsBackupCopy ? "备份" : "原件";

        /// <summary>
        /// 电子介质关联硬盘/光盘展示（与资料详情页「关联介质」语义一致）。
        /// </summary>
        public string LinkedMediumDisplay
        {
            get
            {
                if (!string.Equals(
                        MediaKind,
                        ArchiveRegisterDomainValues.MediaKindElectronic,
                        StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(MediumCode))
                {
                    return MediumCode.Trim();
                }

                if (StorageCarrierType.Contains("光盘", StringComparison.OrdinalIgnoreCase))
                {
                    return "光盘";
                }

                return string.Empty;
            }
        }

        /// <summary>登记介质条目上的介质类型（如纸质、U盘等）。</summary>
        public string RegisterMediaType { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string SubCategory { get; init; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        /// <summary>登记申请上的归档目的。</summary>
        public string ArchivePurpose { get; init; } = string.Empty;

        public string ContentSearchKeyword { get; init; } = string.Empty;

        public string ContentSearchKindFilter { get; init; } = string.Empty;

        public IReadOnlyList<MatchedContentEntryInfo> MatchedContentEntries { get; init; } =
            Array.Empty<MatchedContentEntryInfo>();

        public string MatchedContentEntrySummary =>
            ContentEntrySearchSupport.FormatMatchedSummary(MatchedContentEntries);

        public string LifecycleStatusDisplay => MapLifecycleStatus(LifecycleStatus);

        public string BorrowHintDisplay => string.IsNullOrWhiteSpace(BorrowHintText)
            ? MapBorrowHintLevel(BorrowHintLevel)
            : BorrowHintText;

        /// <summary>资料子项当前库存份数（模拟介质；电子介质恒为 1）。</summary>
        public int StockCopyCount { get; init; } = 1;

        /// <summary>库存份数展示文案（出库预留用，取自登记介质 <c>MediaCount</c>）。</summary>
        public string StockCopyCountDisplay { get; init; } = string.Empty;

        /// <summary>
        /// 检索列表份数展示：模拟介质为「当前库内/立档」，电子介质取 <see cref="StockCopyCountDisplay"/>。
        /// </summary>
        public string FilingCopyCountDisplay => string.Equals(
            MediaKind,
            ArchiveRegisterDomainValues.MediaKindSimulated,
            StringComparison.Ordinal)
            ? $"{Math.Max(0, CurrentInArchiveCopyCount)}/{(ContentCount > 0 ? ContentCount : 1)}"
            : StockCopyCountDisplay;

        public bool IsBorrowHintHighlighted =>
            BorrowHintLevel is FilingFactBorrowHintLevel.OriginalBorrowed
                or FilingFactBorrowHintLevel.CopyBorrowed;

        private static string MapLifecycleStatus(string status) => status switch
        {
            FilingFactLifecycleStatus.InArchive => "在库",
            FilingFactLifecycleStatus.Borrowed => "借出中",
            FilingFactLifecycleStatus.Transferred => "已转移",
            FilingFactLifecycleStatus.Destroyed => "已销毁",
            FilingFactLifecycleStatus.Disposed => "已处置",
            _ => status
        };

        private static string MapBorrowHintLevel(string level) => level switch
        {
            FilingFactBorrowHintLevel.None => string.Empty,
            FilingFactBorrowHintLevel.CopyBorrowed => "有拷贝借出",
            FilingFactBorrowHintLevel.OriginalBorrowed => "原件借出中",
            FilingFactBorrowHintLevel.PartialAvailable => "多备份，部分在库",
            FilingFactBorrowHintLevel.Unknown => "借出状态待确认",
            _ => string.Empty
        };
    }

    public sealed class RegisterDirectionSearchCriteria
    {
        public string? Year { get; set; }

        public int? ProjectId { get; set; }

        public string Keyword { get; set; } = string.Empty;

        public string ConfidentialLevel { get; set; } = string.Empty;

        /// <summary>
        /// 目录/文件名称关键词（仅电子介质）。
        /// 支持通配符：<c>*</c> 任意字符、<c>?</c> 单字符；不含通配符时为包含匹配。
        /// </summary>
        public string ContentEntryKeyword { get; set; } = string.Empty;

        /// <summary>
        /// 条目类型过滤：空表示全部；<see cref="ArchiveRegisterDomainValues.ElectronicEntryKindDirectory"/> 或
        /// <see cref="ArchiveRegisterDomainValues.ElectronicEntryKindFile"/>。
        /// </summary>
        public string ContentEntryKindFilter { get; set; } = string.Empty;

        public string? LifecycleStatus { get; set; }

        public DateTime? FiledFrom { get; set; }

        public DateTime? FiledTo { get; set; }
    }

    public sealed class ContainerDirectionSearchCriteria
    {
        public string? Year { get; set; }

        public string ContainerCode { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        public string MediumCode { get; set; } = string.Empty;

        public string? StorageCarrierType { get; set; }

        public string Keyword { get; set; } = string.Empty;

        public bool SearchCurrentLocation { get; set; }
    }

    public sealed class ArchiveSearchPoolSelection
    {
        public int FilingFactId { get; init; }

        public string SelectionScopeKind { get; init; } = ArchiveSearchSelectionScopeKind.WholeMediaItem;

        public int? ContentEntryId { get; init; }

        /// <summary>筛选池申请份数（模拟介质整子项；默认 1）。</summary>
        public int RequestedCopyCount { get; set; } = 1;

        public bool IsWholeMediaItem =>
            string.Equals(
                SelectionScopeKind,
                ArchiveSearchSelectionScopeKind.WholeMediaItem,
                StringComparison.Ordinal);

        public bool IsContentEntry =>
            string.Equals(
                SelectionScopeKind,
                ArchiveSearchSelectionScopeKind.ContentEntry,
                StringComparison.Ordinal)
            && ContentEntryId is > 0;
    }

    public sealed class SaveArchiveSearchResultSetRequest
    {
        public string Name { get; set; } = string.Empty;

        public string Remarks { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;

        public IReadOnlyList<ArchiveSearchPoolSelection> Selections { get; set; } =
            Array.Empty<ArchiveSearchPoolSelection>();
    }

    /// <summary>
    /// 立档台账查询条件（跨模拟/电子介质，按立档年度浏览）。
    /// </summary>
    public sealed class FilingLedgerSearchCriteria
    {
        public string? Year { get; set; }

        public int? ProjectId { get; set; }

        /// <summary>空表示全部；<see cref="ArchiveRegisterDomainValues.MediaKindSimulated"/> 或 <see cref="ArchiveRegisterDomainValues.MediaKindElectronic"/>。</summary>
        public string MediaKind { get; set; } = string.Empty;

        public string Keyword { get; set; } = string.Empty;

        public string? LifecycleStatus { get; set; }

        /// <summary>空表示全部；<see cref="FilingFactArchiveCopyRole.Original"/> 或 <see cref="FilingFactArchiveCopyRole.Backup"/>。</summary>
        public string ArchiveCopyRole { get; set; } = string.Empty;

        public DateTime? FiledFrom { get; set; }

        public DateTime? FiledTo { get; set; }

        /// <summary>指定立档事实 Id 时精确跳转定位（忽略其他筛选）。</summary>
        public int? FilingFactId { get; set; }
    }

    /// <summary>
    /// 立档台账详情中的目录/文件明细行。
    /// </summary>
    public sealed class FilingLedgerContentEntryInfo
    {
        public string EntryKind { get; init; } = string.Empty;

        public string EntryName { get; init; } = string.Empty;

        /// <summary>立档时写入目标介质的条目路径。</summary>
        public string FilingPath { get; init; } = string.Empty;

        public string CreatedDateText { get; init; } = string.Empty;

        public string ModifiedDateText { get; init; } = string.Empty;

        public string SizeText { get; init; } = string.Empty;
    }

    /// <summary>
    /// 立档台账列表行（含立档事实完整展示字段）。
    /// </summary>
    public sealed class FilingLedgerRow
    {
        public int FilingFactId { get; init; }

        public string FilingFactNo { get; init; } = string.Empty;

        public string MediaKind { get; init; } = string.Empty;

        /// <summary>登记介质条目上的介质类型（如纸质、U盘等）。</summary>
        public string RegisterMediaType { get; init; } = string.Empty;

        public string MaterialCategory { get; init; } = string.Empty;

        public string SubCategory { get; init; } = string.Empty;

        public string DataOrganizationForm { get; init; } = string.Empty;

        /// <summary>登记申请上的归档目的。</summary>
        public string ArchivePurpose { get; init; } = string.Empty;

        public int RegisterRecordId { get; init; }

        public int RegisterMediaId { get; init; }

        public int MediaItemId { get; init; }

        public string FormNo { get; init; } = string.Empty;

        public string MaterialName { get; init; } = string.Empty;

        public string ProjectName { get; init; } = string.Empty;

        public string ProvideUnit { get; init; } = string.Empty;

        public string ApplicantName { get; init; } = string.Empty;

        public string ItemType { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public string ConfidentialLevel { get; init; } = string.Empty;

        public int ContentCount { get; init; }

        /// <summary>模拟介质当前库内份数（已扣待还/不还/灭失）。</summary>
        public int CurrentInArchiveCopyCount { get; init; }

        /// <summary>模拟介质累计灭失份数。</summary>
        public int LostCopyCount { get; init; }

        /// <summary>模拟介质出库待还份数。</summary>
        public int PendingReturnCopyCount { get; init; }

        /// <summary>模拟介质出库不还份数。</summary>
        public int NoReturnCopyCount { get; init; }

        public ArchiveContainerKind ContainerKind { get; init; }

        public string ContainerCode { get; init; } = string.Empty;

        public string StorageLocation { get; init; } = string.Empty;

        public string CabinetName { get; init; } = string.Empty;

        public string BoxLocationCode { get; init; } = string.Empty;

        public string BoxSpecs { get; init; } = string.Empty;

        public string StorageCarrierType { get; init; } = string.Empty;

        public string Disposition { get; init; } = string.Empty;

        public string MediumCode { get; init; } = string.Empty;

        public string FilingStoragePath { get; init; } = string.Empty;

        public decimal DataSizeMb { get; init; }

        public DateTime FiledAt { get; init; }

        public string FiledBy { get; init; } = string.Empty;

        public string LifecycleStatus { get; init; } = FilingFactLifecycleStatus.InArchive;

        public string CurrentContainerCode { get; init; } = string.Empty;

        public string CurrentStorageLocation { get; init; } = string.Empty;

        public DateTime? LifecycleUpdatedAt { get; init; }

        public string LifecycleRemark { get; init; } = string.Empty;

        public string BorrowHintLevel { get; init; } = FilingFactBorrowHintLevel.None;

        public string BorrowHintText { get; init; } = string.Empty;

        public int? PrimaryFilingFactId { get; init; }

        public string ArchiveCopyRole { get; init; } = FilingFactArchiveCopyRole.Original;

        public string FilingYear => FiledAt.Year.ToString();

        public string FiledAtDisplay => FiledAt.ToString("yyyy-MM-dd HH:mm");

        public string DataSizeDisplay => DataSizeMb > 0 ? $"{DataSizeMb:0.##} MB" : string.Empty;

        /// <summary>模拟介质份数展示：当前库内/立档；含灭失时附加说明。</summary>
        public string CopyCountStatusDisplay
        {
            get
            {
                if (!IsSimulatedMedia)
                {
                    return ContentCount > 0 ? $"{ContentCount}" : "—";
                }

                int filed = ContentCount > 0 ? ContentCount : 1;
                string text = $"{Math.Max(0, CurrentInArchiveCopyCount)}/{filed}";
                if (LostCopyCount > 0)
                {
                    text += $"（灭失{LostCopyCount}）";
                }

                return text;
            }
        }

        public string ContainerKindDisplay => ContainerKind switch
        {
            ArchiveContainerKind.ArchiveBox => "档案盒",
            ArchiveContainerKind.ElectronicBag => "电子介质袋",
            _ => ContainerKind.ToString()
        };

        public string LifecycleStatusDisplay => MapLifecycleStatus(LifecycleStatus);

        public string ArchiveCopyRoleDisplay => string.Equals(
            ArchiveCopyRole,
            FilingFactArchiveCopyRole.Backup,
            StringComparison.Ordinal)
            ? "备份"
            : "原件";

        public string BorrowHintDisplay => string.IsNullOrWhiteSpace(BorrowHintText)
            ? MapBorrowHintLevel(BorrowHintLevel)
            : BorrowHintText;

        public bool IsElectronicMedia => string.Equals(
            MediaKind,
            ArchiveRegisterDomainValues.MediaKindElectronic,
            StringComparison.Ordinal);

        public bool IsSimulatedMedia => !IsElectronicMedia;

        /// <summary>立档容器编号与当前容器编号是否不同。</summary>
        public bool HasContainerChanged => !EqualsNormalized(ContainerCode, CurrentContainerCode);

        /// <summary>立档存放位置与当前存放位置是否不同。</summary>
        public bool HasStorageLocationChanged =>
            !EqualsNormalized(StorageLocation, CurrentStorageLocation);

        /// <summary>容器或存放位置任一相对立档快照发生变化。</summary>
        public bool HasCurrentStorageChanged => HasContainerChanged || HasStorageLocationChanged;

        /// <summary>盒位置编码与立档存放位置不同且非空时，才单独展示盒位置编码。</summary>
        public bool ShowDistinctBoxLocationCode =>
            !string.IsNullOrWhiteSpace(BoxLocationCode)
            && !EqualsNormalized(BoxLocationCode, StorageLocation);

        /// <summary>当前存放与立档快照一致时的提示文案。</summary>
        public string CurrentStorageUnchangedHint => "与立档时一致";

        private static bool EqualsNormalized(string? left, string? right)
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
        }

        private static string MapLifecycleStatus(string status) => status switch
        {
            FilingFactLifecycleStatus.InArchive => "在库",
            FilingFactLifecycleStatus.Borrowed => "借出中",
            FilingFactLifecycleStatus.Transferred => "已转移",
            FilingFactLifecycleStatus.Destroyed => "已销毁",
            FilingFactLifecycleStatus.Disposed => "已处置",
            _ => status
        };

        private static string MapBorrowHintLevel(string level) => level switch
        {
            FilingFactBorrowHintLevel.None => string.Empty,
            FilingFactBorrowHintLevel.CopyBorrowed => "有拷贝借出",
            FilingFactBorrowHintLevel.OriginalBorrowed => "原件借出中",
            FilingFactBorrowHintLevel.PartialAvailable => "多备份，部分在库",
            FilingFactBorrowHintLevel.Unknown => string.Empty,
            _ => string.Empty
        };
    }
}
