namespace DocMgr.Models.OpticalDiscMedia
{
    /// <summary>
    /// 光盘概览 KPI 卡片跳转目标。
    /// </summary>
    public enum OpticalDiscOverviewKpiKind
    {
        TotalMedia = 0,
        InStock = 1,
        OutTemporary = 2,
        DamagedInStock = 3,
        Destroyed = 4,
        NeedReturn = 5,
        MissingLocation = 6,
        RecentTransactions = 7,
        OutboundWithoutKeeper = 8,
        LostInStock = 9,
        ScrapInStock = 10
    }

    /// <summary>
    /// 光盘流转台账快捷筛选（概览下钻用）。
    /// </summary>
    public enum OpticalDiscLedgerQuickFilter
    {
        None = 0,
        NeedReturn = 1,
        MissingLocation = 2,
        OutboundWithoutKeeper = 3
    }
}
