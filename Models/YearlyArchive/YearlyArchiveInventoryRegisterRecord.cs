using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DocMgr.Models.Shared;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 年度资料盘库登记单（主表；轻量草稿/办结/作废）。
    /// </summary>
    [Table("YearlyArchiveInventoryRegisterRecords")]
    public sealed class YearlyArchiveInventoryRegisterRecord
    {
        public const int StatusDraft = ApplicationWorkflowStatus.Draft;
        public const int StatusCompleted = ApplicationWorkflowStatus.Completed;
        public const int StatusWithdrawn = ApplicationWorkflowStatus.Withdrawn;

        [Key]
        public int Id { get; set; }

        /// <summary>登记单编号。</summary>
        [Required]
        public string RegisterNo { get; set; } = string.Empty;

        /// <summary>工作流状态（草稿/已办结/已撤回作废）。</summary>
        public int Status { get; set; } = StatusDraft;

        /// <summary>介质轨：模拟 / 电子。</summary>
        public string MediaKind { get; set; } = string.Empty;

        /// <summary>登记类型（整单唯一）：盘失登记 / 损坏登记 / 拟销登记。</summary>
        public string RegisterKind { get; set; } = string.Empty;

        /// <summary>登记说明。</summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>备注。</summary>
        public string Remark { get; set; } = string.Empty;

        public int ApplicantUserId { get; set; }

        public string ApplicantName { get; set; } = string.Empty;

        public string ApplicantDept { get; set; } = string.Empty;

        public DateTime ApplyTime { get; set; }

        public DateTime? CompletedAt { get; set; }

        public string CompletedBy { get; set; } = string.Empty;

        public DateTime? WithdrawnAt { get; set; }

        public string WithdrawReason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<YearlyArchiveInventoryRegisterItem> Items { get; set; } = new List<YearlyArchiveInventoryRegisterItem>();

        [NotMapped]
        public string StatusDisplay => ArchiveInventoryRegisterDomainValues.ToStatusDisplay(Status);

        [NotMapped]
        public int ItemCount => Items?.Count ?? 0;

        /// <summary>模拟轨：盒号清单（去重，逗号分隔）。</summary>
        [NotMapped]
        public string SimulatedBoxSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ContainerCode?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.Ordinal));

        /// <summary>模拟轨：档口清单（去重，逗号分隔）。</summary>
        [NotMapped]
        public string SimulatedSlotSummary => BuildSlotSummary();

        /// <summary>模拟轨：资料明细清单（资料名/子项名，逗号分隔）。</summary>
        [NotMapped]
        public string SimulatedItemSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i =>
                    {
                        string material = i.MaterialName?.Trim() ?? string.Empty;
                        string itemName = i.ItemName?.Trim() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(material) && !string.IsNullOrWhiteSpace(itemName))
                        {
                            return $"{material}/{itemName}";
                        }

                        return !string.IsNullOrWhiteSpace(itemName) ? itemName : material;
                    })
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>模拟轨：丢失份数展示（拟销登记显示「-」）。</summary>
        [NotMapped]
        public string SimulatedLostCopySummary =>
            string.Equals(RegisterKind?.Trim(), ArchiveInventoryRegisterDomainValues.KindScrap, StringComparison.Ordinal)
                ? "-"
                : SimulatedLostTotal.ToString();

        /// <summary>模拟轨：丢失份数合计。</summary>
        [NotMapped]
        public int SimulatedLostTotal =>
            Items?.Sum(i => i.LostCopyCount) ?? 0;

        /// <summary>电子轨：介质类别清单（去重，逗号分隔）。</summary>
        [NotMapped]
        public string ElectronicMediumKindSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.MediumKind?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.Ordinal));

        /// <summary>电子轨：介质编号清单（光盘显示「-」，逗号分隔）。</summary>
        [NotMapped]
        public string ElectronicMediumSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => ArchiveInventoryRegisterDomainValues.ResolveMediumCodeDisplay(i.MediumKind, i.MediumCode))
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

        /// <summary>电子轨：电子袋编号清单（去重，逗号分隔）。</summary>
        [NotMapped]
        public string ElectronicArchiveBagSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.ElectronicArchiveNo?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.Ordinal));

        /// <summary>电子轨：介质状态清单（去重，逗号分隔）。</summary>
        [NotMapped]
        public string ElectronicMediaStatusSummary =>
            Items == null || Items.Count == 0
                ? string.Empty
                : string.Join("、", Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => i.BeforeMediaStatus?.Trim() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.Ordinal));

        /// <summary>电子轨：档口清单（去重，逗号分隔）。</summary>
        [NotMapped]
        public string ElectronicSlotSummary => BuildSlotSummary();

        [NotMapped]
        public bool IsCompleted => Status == StatusCompleted;

        private string BuildSlotSummary()
        {
            if (Items == null || Items.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("、", Items
                .OrderBy(i => i.SortOrder)
                .Select(i => i.BeforeStorageLocation?.Trim() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }
}
