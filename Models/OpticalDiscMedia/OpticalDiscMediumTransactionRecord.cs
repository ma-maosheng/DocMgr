namespace DocMgr.Models.OpticalDiscMedia
{
    /// <summary>
    /// 光盘台账流转记录展示模型。
    /// </summary>
    public sealed class OpticalDiscMediumTransactionRecord
    {
        /// <summary>
        /// 流转记录主键。
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// 光盘主表 ID。
        /// </summary>
        public int MediumId { get; init; }

        /// <summary>
        /// 光盘编号。
        /// </summary>
        public string DiscCode { get; init; } = string.Empty;

        /// <summary>
        /// 流转类型。
        /// </summary>
        public string TransactionType { get; init; } = string.Empty;

        /// <summary>
        /// 业务单号。
        /// </summary>
        public string BusinessNo { get; init; } = string.Empty;

        /// <summary>
        /// 流转前状态。
        /// </summary>
        public string BeforeStatus { get; init; } = string.Empty;

        /// <summary>
        /// 流转后状态。
        /// </summary>
        public string AfterStatus { get; init; } = string.Empty;

        /// <summary>
        /// 前位置。
        /// </summary>
        public string BeforeLocation { get; init; } = string.Empty;

        /// <summary>
        /// 后位置。
        /// </summary>
        public string AfterLocation { get; init; } = string.Empty;

        /// <summary>
        /// 办理人。
        /// </summary>
        public string OperatorName { get; init; } = string.Empty;

        /// <summary>
        /// 办理时间。
        /// </summary>
        public DateTime OperateTime { get; init; }

        /// <summary>
        /// 业务说明。
        /// </summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; init; } = string.Empty;
    }
}
