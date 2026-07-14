namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质模块首页概览。
    /// </summary>
    public class HardDiskMediaOverview
    {
        /// <summary>
        /// 介质总数。
        /// </summary>
        public int TotalMediumCount { get; set; }

        /// <summary>
        /// 在库空白数量。
        /// </summary>
        public int BlankInStockCount { get; set; }

        /// <summary>
        /// 借出中数量。
        /// </summary>
        public int BorrowedCount { get; set; }

        /// <summary>
        /// 资料在库数量。
        /// </summary>
        public int DataCarrierInStockCount { get; set; }

        /// <summary>
        /// 损坏在库数量。
        /// </summary>
        public int DamagedInStockCount { get; set; }

        /// <summary>
        /// 对外移交数量。
        /// </summary>
        public int TransferOutCount { get; set; }

        /// <summary>
        /// 需归还介质数量。
        /// </summary>
        public int NeedReturnMediumCount { get; set; }

        /// <summary>
        /// 长期借出且需归还数量。
        /// </summary>
        public int LongTermNeedReturnMediumCount { get; set; }

        /// <summary>
        /// 临时借出且需归还数量。
        /// </summary>
        public int TemporaryNeedReturnMediumCount { get; set; }

        /// <summary>
        /// 未登记位置数量。
        /// </summary>
        public int MissingLocationMediumCount { get; set; }

        /// <summary>
        /// 出库未明确保管数量。
        /// </summary>
        public int OutboundWithoutKeeperMediumCount { get; set; }

        /// <summary>
        /// 待办理申请数量。
        /// </summary>
        public int PendingProcessApplicationCount { get; set; }

        /// <summary>
        /// 待上传签字件申请数量。
        /// </summary>
        public int PendingSignedFileCount { get; set; }

        /// <summary>
        /// 待审批申请数量。
        /// </summary>
        public int SubmittedApplicationCount { get; set; }

        /// <summary>
        /// 位置分布分析。
        /// </summary>
        public IReadOnlyList<string> LocationInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 出库容量分析。
        /// </summary>
        public IReadOnlyList<string> OutboundCapacityInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 交接环节分析。
        /// </summary>
        public IReadOnlyList<string> HandoverInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 生命周期结构分析。
        /// </summary>
        public IReadOnlyList<string> LifecycleInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 风险提示分析。
        /// </summary>
        public IReadOnlyList<string> RiskInsights { get; set; } = Array.Empty<string>();
    }
}
