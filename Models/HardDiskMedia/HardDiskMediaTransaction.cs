using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质流转记录。
    /// </summary>
    public class HardDiskMediaTransaction
    {
        public const string TypeRegister = "登记";
        public const string TypeOutboundTemporary = "出库(临时)";
        public const string TypeOutboundLongTerm = "出库(长期)";
        public const string TypeOutboundPermanent = "出库(永久)";
        public const string TypeDisposal = "离库(处置)";
        public const string TypeReturnRegistration = "归还登记";
        public const string TypeLossRegistration = "挂失登记";
        public const string TypeInventoryLost = "在库(盘失)";
        public const string TypeInventoryRegisterDamage = "盘库登记(损坏)";
        public const string TypeInventoryRegisterLost = "盘库登记(盘失)";
        public const string TypeInventoryRegisterScrap = "盘库登记(拟销)";
        public const string TypeInventoryRegisterRelocate = "盘库登记(调档)";
        public const string TypeRelocate = "位置调整";

        public const string TypeBorrow = TypeOutboundTemporary;
        public const string TypeReturn = TypeReturnRegistration;
        public const string TypeConvertCarrier = TypeReturnRegistration;
        public const string TypeInStockCarrier = TypeReturnRegistration;
        public const string TypeTransferOut = TypeOutboundPermanent;

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 介质主表ID。
        /// </summary>
        public int MediumId { get; set; }

        /// <summary>
        /// 关联申请单ID。
        /// </summary>
        public int? ApplicationId { get; set; }

        /// <summary>
        /// 业务单号。
        /// </summary>
        public string BusinessNo { get; set; } = string.Empty;

        /// <summary>
        /// 展示用业务单号。无真实业务单时显示「-」（不回退到相关批次/硬盘编号）。
        /// </summary>
        [NotMapped]
        public string BusinessNoDisplay
        {
            get
            {
                string? resolved = ResolveViewableBusinessNo();
                return string.IsNullOrWhiteSpace(resolved) ? "-" : resolved;
            }
        }

        /// <summary>
        /// 是否存在可打开详情的真实业务单号。
        /// </summary>
        [NotMapped]
        public bool HasViewableBusinessNo => !string.IsNullOrWhiteSpace(ResolveViewableBusinessNo());

        /// <summary>
        /// 解析可查看的业务单号；交互式/批量迁档等无独立单号时返回 null。
        /// </summary>
        public string? ResolveViewableBusinessNo()
        {
            if (Application != null && !string.IsNullOrWhiteSpace(Application.ApplicationNo))
            {
                return Application.ApplicationNo.Trim();
            }

            if (string.IsNullOrWhiteSpace(BusinessNo))
            {
                return null;
            }

            string trimmed = BusinessNo.Trim();
            if (IsDiskCodePlaceholderBusinessNo(trimmed))
            {
                return null;
            }

            return trimmed;
        }

        /// <summary>
        /// 位置调整且无申请单时，历史上曾用硬盘编号充业务单号；此类不可作业务详情入口。
        /// </summary>
        private bool IsDiskCodePlaceholderBusinessNo(string businessNo)
        {
            if (!string.Equals(TransactionType, TypeRelocate, StringComparison.Ordinal)
                || ApplicationId.HasValue)
            {
                return false;
            }

            if (Medium != null
                && !string.IsNullOrWhiteSpace(Medium.DiskCode)
                && string.Equals(businessNo, Medium.DiskCode.Trim(), StringComparison.Ordinal))
            {
                return true;
            }

            return ContainsCabinetRelocationHint(Description) || ContainsCabinetRelocationHint(Remark);
        }

        private static bool ContainsCabinetRelocationHint(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.Contains("交互式迁档", StringComparison.Ordinal)
                || text.Contains("档口批量搬迁", StringComparison.Ordinal)
                || text.Contains("单件迁档", StringComparison.Ordinal);
        }

        /// <summary>
        /// 流转类型。
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// 流转前状态。
        /// </summary>
        public string BeforeStatus { get; set; } = string.Empty;

        /// <summary>
        /// 流转后状态。
        /// </summary>
        public string AfterStatus { get; set; } = string.Empty;

        /// <summary>
        /// 流转前位置。
        /// </summary>
        public string BeforeLocation { get; set; } = string.Empty;

        /// <summary>
        /// 流转后位置。
        /// </summary>
        public string AfterLocation { get; set; } = string.Empty;

        /// <summary>
        /// 经办人。
        /// </summary>
        public string OperatorName { get; set; } = string.Empty;

        /// <summary>
        /// 办理时间。
        /// </summary>
        public DateTime OperateTime { get; set; }

        /// <summary>
        /// 相关人员。
        /// </summary>
        public string RelatedPerson { get; set; } = string.Empty;

        /// <summary>
        /// 目标单位。
        /// </summary>
        public string TargetOrganization { get; set; } = string.Empty;

        /// <summary>
        /// 是否要求归还。
        /// </summary>
        public bool NeedReturn { get; set; }

        /// <summary>
        /// 预计归还日期。
        /// </summary>
        public DateTime? ExpectedReturnDate { get; set; }

        /// <summary>
        /// 实际归还日期。
        /// </summary>
        public DateTime? ActualReturnDate { get; set; }

        /// <summary>
        /// 相关批次。
        /// </summary>
        public string RelatedBatch { get; set; } = string.Empty;

        /// <summary>
        /// 相关资料标题。
        /// </summary>
        public string RelatedArchiveTitle { get; set; } = string.Empty;

        /// <summary>
        /// 业务说明。
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 关联介质。
        /// </summary>
        public virtual HardDiskMedium? Medium { get; set; }

        /// <summary>
        /// 关联申请单。
        /// </summary>
        public virtual HardDiskMediaApplication? Application { get; set; }
    }
}
