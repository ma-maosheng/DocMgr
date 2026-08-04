namespace DocMgr.Models.OpticalDiscMedia
{
    /// <summary>
    /// 数据光盘介质流转记录（持久化台账流水）。
    /// </summary>
    public class OpticalDiscMediaTransaction
    {
        public const string TypeRegister = "登记";
        public const string TypeArchiveInbound = "立档入库";
        public const string TypeOutboundTemporary = "出库(临时)";
        public const string TypeReturnRegistration = "归还登记";
        public const string TypeDamagedRegistration = "损坏登记";
        public const string TypeInventoryRegisterDamage = "盘库登记(损坏)";
        public const string TypeInventoryRegisterLost = "盘库登记(盘失)";
        public const string TypeInventoryRegisterScrap = "盘库登记(拟销)";
        public const string TypeDestroy = "出库(销毁)";
        public const string TypeRelocate = "位置调整";

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 光盘主表 ID。
        /// </summary>
        public int MediumId { get; set; }

        /// <summary>
        /// 关联申请单 ID。
        /// </summary>
        public int? ApplicationId { get; set; }

        /// <summary>
        /// 流转类型。
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// 业务单号。
        /// </summary>
        public string BusinessNo { get; set; } = string.Empty;

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
        /// 关联光盘。
        /// </summary>
        public virtual OpticalDiscMedium? Medium { get; set; }
    }
}
