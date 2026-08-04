using System;

namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 介质归还登记候选项。
    /// </summary>
    public sealed record HardDiskMediaReturnCandidate
    {
        /// <summary>
        /// 介质主键。
        /// </summary>
        public int MediumId { get; init; }

        /// <summary>
        /// 来源硬盘借出申请单主键（介质管理出库申请）。
        /// </summary>
        public int? SourceApplicationId { get; init; }

        /// <summary>
        /// 来源资料出库单主键（库内空盘征用或提档数据硬盘需归还时填写）。
        /// </summary>
        public int? SourceOutboundRecordId { get; init; }

        /// <summary>
        /// 来源借出单编号（硬盘借出申请单号或资料出库单号）。
        /// </summary>
        public string SourceApplicationNo { get; init; } = string.Empty;

        /// <summary>
        /// 申请人。
        /// </summary>
        public string ApplicantName { get; init; } = string.Empty;

        /// <summary>
        /// 申请部门。
        /// </summary>
        public string ApplicantDept { get; init; } = string.Empty;

        /// <summary>
        /// 硬盘编号。
        /// </summary>
        public string DiskCode { get; init; } = string.Empty;

        /// <summary>
        /// 序列号。
        /// </summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>
        /// 容量。
        /// </summary>
        public string Capacity { get; init; } = string.Empty;

        /// <summary>
        /// 接口类型。
        /// </summary>
        public string InterfaceType { get; init; } = string.Empty;

        /// <summary>
        /// 当前借出位置。
        /// </summary>
        public string BorrowedLocation { get; init; } = string.Empty;

        /// <summary>
        /// 原存放位置。
        /// </summary>
        public string OriginalLocation { get; init; } = string.Empty;

        /// <summary>
        /// 当前状态。
        /// </summary>
        public string CurrentStatus { get; init; } = string.Empty;

        /// <summary>
        /// 预计归还日期。
        /// </summary>
        public DateTime? ExpectedReturnDate { get; init; }
    }
}
