namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 硬盘概览 KPI 卡片跳转目标。
    /// </summary>
    public enum HardDiskOverviewKpiKind
    {
        TotalMedia = 0,
        BlankInStock = 1,
        DataInStock = 2,
        Borrowed = 3,
        DamagedInStock = 4,
        InStockLost = 5,
        PermanentTransfer = 6,
        DisposedMedia = 7,
        SubmittedApproval = 8,
        PendingHandover = 9,
        PendingSignedUpload = 10,
        PendingComplete = 11,
        OverdueNeedReturn = 12,
        Locked = 13,
        PendingDisposal = 14,
        DraftInventory = 15,
        NeedReturn = 16,
        MissingLocation = 17,
        MissingLedger = 18,
        OutboundWithoutKeeper = 19
    }

    /// <summary>
    /// 硬盘台账列表快捷筛选（概览下钻用，不依赖单一状态下拉）。
    /// </summary>
    public enum HardDiskLedgerQuickFilter
    {
        None = 0,
        BorrowedTempOrLong = 1,
        NeedReturn = 2,
        MissingLocationInStock = 3,
        MissingLedger = 4,
        OutboundWithoutKeeper = 5
    }
}
