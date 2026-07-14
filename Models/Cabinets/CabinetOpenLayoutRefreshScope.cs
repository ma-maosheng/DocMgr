namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜界面布局刷新范围：用于主界面与档口快照窗体之间的数据同步。
    /// </summary>
    public sealed class CabinetOpenLayoutRefreshScope
    {
        public int CabinetId { get; init; }

        public CabinetFace Face { get; init; }

        /// <summary>
        /// 指定档口编号；为空表示当前面别下全部档口。
        /// </summary>
        public string SlotCode { get; init; } = string.Empty;
    }
}
