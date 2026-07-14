using System.ComponentModel.DataAnnotations.Schema;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质申请单。
    /// </summary>
    public class HardDiskMediaApplication
    {
        public const string StatusDraft = "当前草稿-待提交";
        public const string StatusSubmitted = "已提交-待审批";
        public const string StatusApproved = "已审批-待实物交接";
        public const string StatusSignedUploaded = "已实物交接-待上传签批交接单";
        public const string StatusCompleted = "已办结（业务已闭环）";
        public const string StatusWithdrawn = "已作废（撤回）";
        public const string StatusForceWithdrawn = "已作废（强制）";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusDraft = "未提交";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusSubmitted = "已提交";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusApproved = "已审批";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusSignedUploaded = "已上传签字件";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusCompleted = "已办结";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusWithdrawn = "已撤回作废";

        /// <summary>历史状态值，仅用于启动期数据归一化。</summary>
        public const string LegacyStatusForceWithdrawn = "已强制作废";

        public const string StatusReturned = StatusWithdrawn;
        public const string StatusCancelled = StatusForceWithdrawn;

        public const string StatusPendingUpload = StatusSignedUploaded;
        public const string StatusPendingProcess = StatusSignedUploaded;

        public const string TypeOutboundTemporary = "出库(临时)申请";
        public const string TypeOutboundLongTerm = "出库(长期)申请";
        public const string TypeOutboundPermanent = "出库(永久)申请";
        public const string TypeOutboundDestroy = "出库(销毁)申请";
        public const string TypeReturnBlankRegistration = "归还登记(空盘)";
        public const string TypeReturnDataRegistration = "归还登记(资料)";
        public const string TypeReturnDamagedRegistration = "归还登记(损坏)";
        public const string TypeLossRegistration = "挂失登记";

        public const string TypeBorrow = TypeOutboundTemporary;
        public const string TypeReturn = TypeReturnBlankRegistration;
        public const string TypeConvertCarrier = TypeReturnDataRegistration;
        public const string TypeTransferOut = TypeOutboundPermanent;
        public const string TypeDestroy = TypeOutboundDestroy;
        public const string TypeRelocate = "位置调整申请";

        /// <summary>
        /// 主键。
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 申请单编号。
        /// </summary>
        public string ApplicationNo { get; set; } = string.Empty;

        /// <summary>
        /// 介质主表ID。
        /// </summary>
        public int MediumId { get; set; }

        /// <summary>
        /// 来源借出申请单ID。
        /// </summary>
        public int? SourceApplicationId { get; set; }

        /// <summary>
        /// 来源资料出库单ID（库内空盘征用时填写）。
        /// </summary>
        public int? SourceOutboundRecordId { get; set; }

        /// <summary>
        /// 申请类型。
        /// </summary>
        public string ApplicationType { get; set; } = string.Empty;

        /// <summary>
        /// 申请单状态。
        /// </summary>
        public string ApplicationStatus { get; set; } = StatusDraft;

        /// <summary>
        /// 申请人。
        /// </summary>
        public string ApplicantName { get; set; } = string.Empty;

        /// <summary>
        /// 申请部门。
        /// </summary>
        public string ApplicantDept { get; set; } = string.Empty;

        /// <summary>
        /// 申请时间。
        /// </summary>
        public DateTime ApplyTime { get; set; }

        /// <summary>
        /// 申请原因。
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 对方人员或单位。
        /// </summary>
        public string TargetPersonOrUnit { get; set; } = string.Empty;

        /// <summary>
        /// 当前存放位置。
        /// </summary>
        public string CurrentLocation { get; set; } = string.Empty;

        /// <summary>
        /// 目标位置。
        /// </summary>
        public string TargetLocation { get; set; } = string.Empty;

        /// <summary>
        /// 预计归还日期。
        /// </summary>
        public DateTime? ExpectedReturnDate { get; set; }

        /// <summary>
        /// 相关批次。
        /// </summary>
        public string RelatedBatch { get; set; } = string.Empty;

        /// <summary>
        /// 相关资料标题。
        /// </summary>
        public string RelatedArchiveTitle { get; set; } = string.Empty;

        /// <summary>
        /// 打印次数。
        /// </summary>
        public int PrintCount { get; set; }

        /// <summary>
        /// 最近打印时间。
        /// </summary>
        public DateTime? PrintedTime { get; set; }

        /// <summary>
        /// 是否已上传签字件。
        /// </summary>
        public bool SignedAttachmentUploaded { get; set; }

        /// <summary>
        /// 签字件上传时间。
        /// </summary>
        public DateTime? SignedAttachmentUploadedTime { get; set; }

        /// <summary>
        /// 签字件上传人。
        /// </summary>
        public string SignedAttachmentUploader { get; set; } = string.Empty;

        /// <summary>
        /// 审核人。
        /// </summary>
        public string ReviewerName { get; set; } = string.Empty;

        /// <summary>
        /// 审核时间。
        /// </summary>
        public DateTime? ReviewerDate { get; set; }

        /// <summary>
        /// 审批人。
        /// </summary>
        public string ApprovedBy { get; set; } = string.Empty;

        /// <summary>
        /// 审批时间。
        /// </summary>
        public DateTime? ApprovedTime { get; set; }

        /// <summary>
        /// 审批意见。
        /// </summary>
        public string ApprovalOpinion { get; set; } = string.Empty;

        /// <summary>
        /// 查验结果。
        /// </summary>
        public string InspectionResult { get; set; } = string.Empty;

        /// <summary>
        /// 格式化确认。
        /// </summary>
        public string FormatConfirmation { get; set; } = string.Empty;

        /// <summary>
        /// 办理人。
        /// </summary>
        public string ExecutedBy { get; set; } = string.Empty;

        /// <summary>
        /// 办理时间。
        /// </summary>
        public DateTime? ExecutedTime { get; set; }

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间。
        /// </summary>
        public DateTime CreatedTime { get; set; }

        /// <summary>
        /// 更新时间。
        /// </summary>
        public DateTime UpdatedTime { get; set; }

        /// <summary>
        /// 关联介质。
        /// </summary>
        public virtual HardDiskMedium? Medium { get; set; }

        [NotMapped]
        public string ReturnRegistrationKindText =>
            HardDiskMediaReturnDomainValues.ResolveRegistrationKindDisplay(ApplicationType, InspectionResult);

        [NotMapped]
        public string ReturnRegistrationStageText => ApplicationStatus switch
        {
            StatusDraft => "已登记归还信息",
            StatusSubmitted => "已登记归还信息",
            StatusSignedUploaded => "已上传签字件",
            StatusCompleted => "已办结（业务已闭环）",
            _ => ApplicationStatus
        };

        /// <summary>
        /// 出库审批流程在界面展示用的状态文案（含签批交接单上传后的子状态）。
        /// </summary>
        [NotMapped]
        public string OutboundWorkflowStatusDisplay => ResolveOutboundWorkflowStatusDisplay(ApplicationStatus, SignedAttachmentUploaded);

        /// <summary>
        /// 解析出库审批流程状态展示文案。
        /// </summary>
        public static string ResolveOutboundWorkflowStatusDisplay(string applicationStatus, bool signedAttachmentUploaded)
        {
            if (string.Equals(applicationStatus, StatusSignedUploaded, StringComparison.Ordinal))
            {
                return signedAttachmentUploaded ? "已上传签批交接单-待办结" : StatusSignedUploaded;
            }

            return applicationStatus;
        }
    }
}
