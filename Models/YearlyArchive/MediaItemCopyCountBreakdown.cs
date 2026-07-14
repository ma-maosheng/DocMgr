namespace DocMgr.Models.YearlyArchive

{

    /// <summary>

    /// 资料子项份数分解：立档份数 = 当前库内 + 出库待还 + 出库不还 + 灭失。

    /// </summary>

    public sealed class MediaItemCopyCountBreakdown

    {

        public int FiledCopyCount { get; init; }



        public int CurrentInArchiveCopyCount { get; init; }



        public int PendingReturnCopyCount { get; init; }



        public int NoReturnCopyCount { get; init; }



        public int LostCopyCount { get; init; }



        /// <summary>电子介质简化展示文案。</summary>

        public string ElectronicStockStatusText { get; init; } = string.Empty;



        public bool IsBalanced =>

            FiledCopyCount == CurrentInArchiveCopyCount + PendingReturnCopyCount + NoReturnCopyCount + LostCopyCount;

    }

}

