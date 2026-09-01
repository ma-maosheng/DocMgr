namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘介质模块首页概览（对齐现行台账状态、申请工作流、离库处置、盘库登记与征用锁）。
    /// </summary>
    public class HardDiskMediaOverview
    {
        /// <summary>介质总数（未删除且有台账）。</summary>
        public int TotalMediumCount { get; set; }

        /// <summary>缺台账介质数（未删除但无台账记录）。</summary>
        public int MissingLedgerMediumCount { get; set; }

        /// <summary>在库空白。</summary>
        public int BlankInStockCount { get; set; }

        /// <summary>资料在库。</summary>
        public int DataCarrierInStockCount { get; set; }

        /// <summary>损坏在库。</summary>
        public int DamagedInStockCount { get; set; }

        /// <summary>在库盘失。</summary>
        public int InStockLostCount { get; set; }

        /// <summary>在库拟销（通常由资料盘库写入，再经离库处置清账）。</summary>
        public int InStockScrapCount { get; set; }

        /// <summary>出库中（临时 + 长期）。</summary>
        public int BorrowedCount { get; set; }

        /// <summary>永久移交。</summary>
        public int PermanentTransferCount { get; set; }

        /// <summary>离库处置（介质状态）。</summary>
        public int DisposedCount { get; set; }

        /// <summary>出库挂失。</summary>
        public int OutLostCount { get; set; }

        /// <summary>需归还介质。</summary>
        public int NeedReturnMediumCount { get; set; }

        /// <summary>临时出库且需归还。</summary>
        public int TemporaryNeedReturnMediumCount { get; set; }

        /// <summary>长期出库且需归还。</summary>
        public int LongTermNeedReturnMediumCount { get; set; }

        /// <summary>逾期需归还（已超预计归还日且无有效归还登记）。</summary>
        public int OverdueNeedReturnCount { get; set; }

        /// <summary>未登记位置（在库可定位状态）。</summary>
        public int MissingLocationMediumCount { get; set; }

        /// <summary>出库未明确保管人/接收单位。</summary>
        public int OutboundWithoutKeeperMediumCount { get; set; }

        /// <summary>征用锁占用介质数。</summary>
        public int LockedMediumCount { get; set; }

        /// <summary>已提交待审批申请。</summary>
        public int SubmittedApplicationCount { get; set; }

        /// <summary>已审批待实物交接。</summary>
        public int PendingHandoverApplicationCount { get; set; }

        /// <summary>待上传签批交接单。</summary>
        public int PendingSignedFileCount { get; set; }

        /// <summary>已上传签批、待办结。</summary>
        public int PendingCompleteApplicationCount { get; set; }

        /// <summary>离库处置进行中（已提交至办结前）。</summary>
        public int PendingDisposalCount { get; set; }

        /// <summary>盘库登记草稿。</summary>
        public int DraftInventoryRegisterCount { get; set; }

        /// <summary>位置与保管分布。</summary>
        public IReadOnlyList<string> LocationInsights { get; set; } = Array.Empty<string>();

        /// <summary>出库容量分布。</summary>
        public IReadOnlyList<string> OutboundCapacityInsights { get; set; } = Array.Empty<string>();

        /// <summary>交接与业务环节分析。</summary>
        public IReadOnlyList<string> HandoverInsights { get; set; } = Array.Empty<string>();

        /// <summary>生命周期结构。</summary>
        public IReadOnlyList<string> LifecycleInsights { get; set; } = Array.Empty<string>();

        /// <summary>风险提示。</summary>
        public IReadOnlyList<string> RiskInsights { get; set; } = Array.Empty<string>();
    }
}
