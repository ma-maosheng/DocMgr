using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    [Table("YearlyArchiveOutboundItems")]
    public sealed class YearlyArchiveOutboundItem
    {
        [Key]
        public int Id { get; set; }

        public int OutboundRecordId { get; set; }

        public int SortOrder { get; set; }

        public int FilingFactId { get; set; }

        public int? PrimaryFilingFactId { get; set; }

        public string ArchiveCopyRole { get; set; } = FilingFactArchiveCopyRole.Original;

        public int? SourceResultSetItemId { get; set; }

        /// <summary>登记时来源检索集 Id（快照追溯，非外键引用）。</summary>
        public int? SourceResultSetId { get; set; }

        public int? ItemArchiveYear { get; set; }

        public string ItemProjectName { get; set; } = string.Empty;

        public string SelectionScopeKind { get; set; } = ArchiveSearchSelectionScopeKind.WholeMediaItem;

        public int? ContentEntryId { get; set; }

        public string ContentEntryKind { get; set; } = string.Empty;

        public string ContentEntryName { get; set; } = string.Empty;

        public string ContentEntryRelativePath { get; set; } = string.Empty;

        public string FormNo { get; set; } = string.Empty;

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string ContainerCode { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        public string CurrentStorageLocation { get; set; } = string.Empty;

        /// <summary>
        /// 待归还期间容器状态提示：空 / LocationChanged（盒位已变）/ BoxInvalid（盒已失效）。
        /// </summary>
        public string ContainerStatusHint { get; set; } = ArchiveOutboundDomainValues.ContainerStatusHintNone;

        public string ConfidentialLevel { get; set; } = ArchiveRegisterDomainValues.ConfidentialLevelNone;

        /// <summary>归档目的，取自登记申请快照。</summary>
        public string ArchivePurpose { get; set; } = string.Empty;

        public string MediaKind { get; set; } = string.Empty;

        /// <summary>介质类型：电子介质取自立档事实载体类型；模拟介质取自登记介质类型。</summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>资料室存储所用具体载体子类型（电子：光盘/硬盘；模拟：装订文本等）。</summary>
        public string StorageCarrierType { get; set; } = string.Empty;

        /// <summary>登记库存份数，用于校验拟领用份数。</summary>
        public int StockCopyCount { get; set; } = 1;

        public string UsageMode { get; set; } = ArchiveOutboundDomainValues.UsageModeWithdrawal;

        public bool NeedReturn { get; set; } = true;

        public int? CopyCount { get; set; }

        public decimal? DataSizeMb { get; set; }

        public string ElectronicMediaSource { get; set; } = string.Empty;

        public bool? IsSelfDiskRegistered { get; set; }

        public string ElectronicMediumType { get; set; } = string.Empty;

        public int? RequisitionedMediumId { get; set; }

        public string RequisitionedDiskCode { get; set; } = string.Empty;

        public bool RequisitionedDiskNeedReturn { get; set; }

        public string SelfDiskSerialNo { get; set; } = string.Empty;

        public string SelfDiskCapacity { get; set; } = string.Empty;

        public string SelfDiskCodesJson { get; set; } = string.Empty;

        public string SelfDiskSerialNumbersJson { get; set; } = string.Empty;

        public string ContainerDisposition { get; set; } = string.Empty;

        public string ReservationStatus { get; set; } = ArchiveOutboundDomainValues.SyncEntryPhaseActive;

        /// <summary>本盒/袋需归还时的预计归还日期（与单元领用设置同步）。</summary>
        public DateTime? ExpectedReturnDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public YearlyArchiveOutboundRecord OutboundRecord { get; set; } = null!;

        /// <summary>
        /// 提档数据硬盘编号展示快照（非持久化，由加载出库单时按立档事实回填）。
        /// </summary>
        [NotMapped]
        public string FiledHardDiskCodes { get; set; } = string.Empty;

        [NotMapped]
        public string NeedReturnDisplay =>
            UsageMode == ArchiveOutboundDomainValues.UsageModeWithdrawal
                ? NeedReturn ? "是" : "否"
                : "—";

        [NotMapped]
        public string RequisitionedDiskNeedReturnDisplay =>
            ShowRequisitionedDiskNeedReturn
                ? RequisitionedDiskNeedReturn ? "是" : "否"
                : "—";

        [NotMapped]
        public bool ShowRequisitionedDiskNeedReturn =>
            string.Equals(MediaKind, ArchiveRegisterDomainValues.MediaKindElectronic, StringComparison.Ordinal)
            && UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate
            && string.Equals(ElectronicMediaSource, ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank, StringComparison.Ordinal)
            && RequisitionedMediumId is > 0;

        [NotMapped]
        public string UsageModeDisplay => UsageMode switch
        {
            ArchiveOutboundDomainValues.UsageModeWithdrawal => "提档",
            ArchiveOutboundDomainValues.UsageModeCopy => "复制",
            ArchiveOutboundDomainValues.UsageModeDuplicate => "拷贝",
            _ => UsageMode
        };

        [NotMapped]
        public string ContainerStatusHintDisplay =>
            ArchiveOutboundDomainValues.GetContainerStatusHintDisplay(ContainerStatusHint);

        [NotMapped]
        public string SelectionScopeDisplay =>
            SelectionScopeKind == ArchiveSearchSelectionScopeKind.ContentEntry
                ? string.IsNullOrWhiteSpace(ContentEntryRelativePath)
                    ? ContentEntryName
                    : ContentEntryRelativePath
                : "整资料子项";
    }
}
