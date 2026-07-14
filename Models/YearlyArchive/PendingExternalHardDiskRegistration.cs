namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 外来硬盘临时登记信息。
    /// </summary>
    public sealed record PendingExternalHardDiskRegistration
    {
        /// <summary>
        /// 硬盘编号。
        /// </summary>
        public string DiskCode { get; init; } = string.Empty;

        /// <summary>
        /// 序列号。
        /// </summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// 硬盘类型。
        /// </summary>
        public string DiskType { get; init; } = string.Empty;

        /// <summary>
        /// 品牌。
        /// </summary>
        public string Brand { get; init; } = string.Empty;

        /// <summary>
        /// 容量。
        /// </summary>
        public string Capacity { get; init; } = string.Empty;

        /// <summary>
        /// 接口类型。
        /// </summary>
        public string InterfaceType { get; init; } = string.Empty;

        /// <summary>
        /// 登记人。
        /// </summary>
        public string RegisterPerson { get; init; } = string.Empty;

        /// <summary>
        /// 登记日期。
        /// </summary>
        public DateTime RegisterDate { get; init; }

        /// <summary>
        /// 出厂日期。
        /// </summary>
        public DateTime? FactoryDate { get; init; }

        /// <summary>
        /// 登记方式。
        /// </summary>
        public string RegistrationMethod { get; init; } = string.Empty;

        /// <summary>
        /// 当前存放位置。
        /// </summary>
        public string CurrentLocation { get; init; } = string.Empty;

        /// <summary>
        /// 当前状态。
        /// </summary>
        public string CurrentStatus { get; init; } = string.Empty;

        /// <summary>
        /// 介质属性。
        /// </summary>
        public string MediaNature { get; init; } = string.Empty;

        /// <summary>
        /// 当前持有人或保管单位。
        /// </summary>
        public string CurrentHolder { get; init; } = string.Empty;

        /// <summary>
        /// 当前是否要求归还。
        /// </summary>
        public bool NeedReturn { get; init; }

        /// <summary>
        /// 转为资料载体日期。
        /// </summary>
        public DateTime? DataCarrierFormedDate { get; init; }

        /// <summary>
        /// 所载资料说明。
        /// </summary>
        public string DataDescription { get; init; } = string.Empty;

        /// <summary>
        /// 相关批次。
        /// </summary>
        public string RelatedBatch { get; init; } = string.Empty;

        /// <summary>
        /// 对外移交对象。
        /// </summary>
        public string TransferTarget { get; init; } = string.Empty;

        /// <summary>
        /// 对外移交日期。
        /// </summary>
        public DateTime? TransferDate { get; init; }

        /// <summary>
        /// 备注。
        /// </summary>
        public string Remark { get; init; } = string.Empty;

        /// <summary>
        /// 格式化为空盘后拟入库位置。
        /// </summary>
        public string FormattedBlankTargetLocation { get; init; } = string.Empty;
    }
}
