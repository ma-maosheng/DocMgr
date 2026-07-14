using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料归还单明细：对应一条出库提档明细的收回。借出份数等于出库份数；完好归还份数与灭失份数之和等于借出份数。
    /// </summary>
    [Table("YearlyArchiveReturnItems")]
    public sealed class YearlyArchiveReturnItem
    {
        [Key]
        public int Id { get; set; }

        public int ReturnRecordId { get; set; }

        public int SortOrder { get; set; }

        /// <summary>源出库明细 Id。</summary>
        public int SourceOutboundItemId { get; set; }

        public int FilingFactId { get; set; }

        /// <summary>登记介质 Id（模拟介质归还份数恢复用，快照）。</summary>
        public int RegisterMediaId { get; set; }

        public string MediaKind { get; set; } = string.Empty;

        public string UsageMode { get; set; } = string.Empty;

        /// <summary>借出份数（= 出库借出份数，只读快照）。</summary>
        public int ReturnCopyCount { get; set; } = 1;

        /// <summary>完好归还份数（默认同借出份数）。</summary>
        public int IntactReturnCopyCount { get; set; } = 1;

        /// <summary>灭失份数（= 借出份数 - 完好归还份数）。</summary>
        public int LossCopyCount { get; set; }

        public string MaterialName { get; set; } = string.Empty;

        public string ItemName { get; set; } = string.Empty;

        public string ContainerCode { get; set; } = string.Empty;

        public string StorageLocation { get; set; } = string.Empty;

        /// <summary>
        /// 原盒失效时指定的归还目标档案盒 Id（并入已有盒或新建空盒后写入）。
        /// </summary>
        public int? RehomeTargetBoxId { get; set; }

        /// <summary>归还物状态，见 <see cref="ArchiveReturnDomainValues"/>。</summary>
        public string ItemCondition { get; set; } = ArchiveReturnDomainValues.ConditionComplete;

        public string Remark { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public YearlyArchiveReturnRecord ReturnRecord { get; set; } = null!;

        /// <summary>载体类型（出库快照，仅展示）。</summary>
        [NotMapped]
        public string MediaType { get; set; } = string.Empty;

        /// <summary>具体载体子类型（出库快照，供归还状态选项）。</summary>
        [NotMapped]
        public string StorageCarrierType { get; set; } = string.Empty;

        /// <summary>明细所属资料年度（出库快照，仅展示）。</summary>
        [NotMapped]
        public int? ItemArchiveYear { get; set; }

        /// <summary>明细所属项目名称（出库快照，仅展示）。</summary>
        [NotMapped]
        public string ItemProjectName { get; set; } = string.Empty;

        /// <summary>密级（出库快照，仅展示）。</summary>
        [NotMapped]
        public string ConfidentialLevel { get; set; } = string.Empty;

        /// <summary>选取范围说明（出库快照，仅展示）。</summary>
        [NotMapped]
        public string SelectionScopeDisplay { get; set; } = string.Empty;

        /// <summary>硬盘/空盘信息（出库快照，仅展示）。</summary>
        [NotMapped]
        public string DiskInfo { get; set; } = string.Empty;

        /// <summary>当前盒号（活数据，仅展示）。</summary>
        [NotMapped]
        public string CurrentContainerCode { get; set; } = string.Empty;

        /// <summary>当前盒位（活数据，仅展示）。</summary>
        [NotMapped]
        public string CurrentStorageLocation { get; set; } = string.Empty;

        /// <summary>容器状态种类，见 <see cref="ArchiveReturnContainerAssessment"/>。</summary>
        [NotMapped]
        public string ContainerStatusKind { get; set; } = ArchiveReturnContainerAssessment.StatusOk;

        [NotMapped]
        public string ContainerStatusDisplay { get; set; } = "正常";

        [NotMapped]
        public string ContainerStatusWarning { get; set; } = string.Empty;

        [NotMapped]
        public int? LiveBoxId { get; set; }

        [NotMapped]
        public bool BlocksWithoutRehome { get; set; }

        /// <summary>归还目标盒显示名（编辑态）。</summary>
        [NotMapped]
        public string RehomeTargetBoxDisplay { get; set; } = string.Empty;

        [NotMapped]
        public string ItemConditionDisplay =>
            ArchiveReturnDomainValues.GetConditionDisplay(ItemCondition, MediaKind, StorageCarrierType);

        [NotMapped]
        public string UsageModeDisplay => UsageMode switch
        {
            ArchiveOutboundDomainValues.UsageModeWithdrawal => "提档",
            ArchiveOutboundDomainValues.UsageModeCopy => "复制",
            ArchiveOutboundDomainValues.UsageModeDuplicate => "拷贝",
            _ => UsageMode
        };

        [NotMapped]
        public string ConfidentialLevelDisplay
        {
            get
            {
                string normalized = ArchiveRegisterDomainValues.NormalizeConfidentialLevel(ConfidentialLevel);
                return string.Equals(normalized, ArchiveRegisterDomainValues.ConfidentialLevelNone, StringComparison.Ordinal)
                    ? string.Empty
                    : normalized;
            }
        }
    }
}
