namespace DocMgr.Models.OpticalDiscMedia
{
    /// <summary>
    /// 数据光盘介质模块首页概览。
    /// </summary>
    public class OpticalDiscMediaOverview
    {
        /// <summary>
        /// 光盘总数（仅含立档写入数据的数据光盘）。
        /// </summary>
        public int TotalMediumCount { get; set; }

        /// <summary>
        /// 在库(资料)数量。
        /// </summary>
        public int InStockCount { get; set; }

        /// <summary>
        /// 出库(临时)数量。
        /// </summary>
        public int OutTemporaryCount { get; set; }

        /// <summary>
        /// 在库(损坏)数量。
        /// </summary>
        public int DamagedInStockCount { get; set; }

        /// <summary>
        /// 在库(盘失)数量，通常由电子资料盘库写入。
        /// </summary>
        public int LostInStockCount { get; set; }

        /// <summary>
        /// 在库(拟销)数量，通常由电子资料盘库写入。
        /// </summary>
        public int ScrapInStockCount { get; set; }

        /// <summary>
        /// 出库(销毁)数量。
        /// </summary>
        public int DestroyedCount { get; set; }

        /// <summary>
        /// 需归还数量。
        /// </summary>
        public int NeedReturnMediumCount { get; set; }

        /// <summary>
        /// 未登记位置数量。
        /// </summary>
        public int MissingLocationMediumCount { get; set; }

        /// <summary>
        /// 出库但未明确保管人/单位数量。
        /// </summary>
        public int OutboundWithoutKeeperMediumCount { get; set; }

        /// <summary>
        /// 近90天流转次数。
        /// </summary>
        public int RecentTransactionCount { get; set; }

        /// <summary>
        /// 位置与保管分布。
        /// </summary>
        public IReadOnlyList<string> LocationInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 生命周期结构。
        /// </summary>
        public IReadOnlyList<string> LifecycleInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 近期流转分析。
        /// </summary>
        public IReadOnlyList<string> CirculationInsights { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 风险提示。
        /// </summary>
        public IReadOnlyList<string> RiskInsights { get; set; } = Array.Empty<string>();
    }
}
