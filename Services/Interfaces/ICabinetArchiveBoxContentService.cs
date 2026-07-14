using DocMgr.Models.Cabinets;



namespace DocMgr.Services.Interfaces

{

    /// <summary>

    /// 档案盒 / 电子介质袋内容查询服务。

    /// </summary>

    public interface ICabinetArchiveBoxContentService

    {

        /// <summary>

        /// 按档案盒物理位置编号获取盒内内容；年度资料按资料子项展示份数分解，历史存档维持原状。

        /// </summary>

        IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetContents(string boxCode);



        /// <summary>按电子介质袋 Id 获取袋内资料子项。</summary>

        IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetElectronicBagContents(int electronicArchiveUnitId);



        /// <summary>按物理位置编号获取电子介质袋内资料子项。</summary>

        IReadOnlyList<CabinetArchiveBoxContentDescriptor> GetElectronicBagContentsByLocation(string storageLocationCode);



        /// <summary>指定物理位置是否为在用的年度资料档案盒。</summary>

        bool IsYearlyArchiveBoxAtLocation(string boxCode);



        /// <summary>指定物理位置是否为在用的年度电子介质袋。</summary>

        bool IsElectronicArchiveBagAtLocation(string storageLocationCode);



        /// <summary>读取电子介质袋级摘要信息。</summary>

        CabinetElectronicArchiveBagHeader? GetElectronicBagHeader(int electronicArchiveUnitId);

        CabinetElectronicArchiveBagHeader? GetElectronicBagHeaderByLocation(string storageLocationCode);

        /// <summary>读取档案盒容器级占用说明（出库预订等）。</summary>
        CabinetArchiveContainerOccupationLockSummary GetArchiveBoxOccupationLockSummary(string boxCode);

        /// <summary>读取电子介质袋容器级占用说明（出库预订、硬盘占用锁等）。</summary>
        CabinetArchiveContainerOccupationLockSummary GetElectronicBagOccupationLockSummary(int electronicArchiveUnitId);

        CabinetArchiveContainerOccupationLockSummary GetElectronicBagOccupationLockSummaryByLocation(string storageLocationCode);

        /// <summary>读取年度模拟档案盒内待还资料追溯明细。</summary>
        IReadOnlyList<SimulatedArchiveBoxPendingReturnDetailRow> GetSimulatedArchiveBoxPendingReturnDetails(string boxCode);
    }

}


