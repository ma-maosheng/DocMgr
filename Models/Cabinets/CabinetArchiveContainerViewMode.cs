namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜「查看档案内容」窗体的展示模式。
    /// </summary>
    public enum CabinetArchiveContainerViewMode
    {
        /// <summary>历史存档档案盒。</summary>
        HistoryArchiveBox = 0,

        /// <summary>年度资料模拟介质档案盒（按资料子项与份数分解）。</summary>
        SimulatedArchiveBox = 1,

        /// <summary>年度资料电子介质袋（按资料子项与电子明细）。</summary>
        ElectronicArchiveBag = 2,
    }
}
