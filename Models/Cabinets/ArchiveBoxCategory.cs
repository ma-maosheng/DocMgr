namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 档案盒资料类别（开柜视图盒边框配色依据）。
    /// </summary>
    public enum ArchiveBoxCategory
    {
        /// <summary>未分类：多来源、历史存档未登记来源等。</summary>
        Unknown = 0,

        /// <summary>历史存档地形图图件。</summary>
        HistoryTopoMap = 1,

        /// <summary>历史存档航摄胶片、像片。</summary>
        HistoryAerialPhoto = 2,

        /// <summary>历史存档其他资料。</summary>
        HistoryOtherMap = 3,

        /// <summary>年度存档资料。</summary>
        Yearly = 4
    }
}
