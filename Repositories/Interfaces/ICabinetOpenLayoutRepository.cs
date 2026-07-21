using DocMgr.Models.Cabinets;
using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.OpticalDiscMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 开柜布局数据访问契约：读取档口布局、占用与位置数据。
/// </summary>
public interface ICabinetOpenLayoutRepository
{
    Dictionary<string, ArchiveBoxSpecification> GetArchiveBoxSpecificationLookup();

    CabinetSlotSpecification? GetCabinetSlotSpecification(string cabinetTypeCode);

    Dictionary<string, CabinetArchiveBoxPlacement> GetPlacementLookup(string cabinetName);

    Cabinet? GetCabinetByIdOrName(int cabinetId, string cabinetName);

    Dictionary<string, string> GetHardDiskSlotCategoryLookup(int cabinetId);

    Dictionary<string, string> GetArchiveSlotCategoryLookup(int cabinetId);

    List<HardDiskMedium> GetHardDiskMediaWithLedger();

    List<HardDiskMediaTransaction> GetHardDiskMediaTransactionsByMediumIds(IReadOnlyCollection<int> mediumIds);

    List<OpticalDiscMedium> GetInStockOpticalDiscMedia();

    List<OpticalDiscMedium> GetOpticalDiscMediaWithLedger();

    List<OpticalDiscMediaTransaction> GetOpticalDiscMediaTransactionsByMediumIds(IReadOnlyCollection<int> mediumIds);

    List<YearlyElectronicArchiveUnitMediumLink> GetElectronicArchiveUnitMediumLinksByMediumIds(IReadOnlyCollection<int> mediumIds);

    List<YearlyElectronicArchiveUnitDiscLink> GetElectronicArchiveUnitDiscLinksByMediumIds(IReadOnlyCollection<int> mediumIds);

    Dictionary<string, decimal> GetUsedDataSizeMbByMediumCodes(IReadOnlyCollection<string> mediumCodes);

    List<TopoMap> GetTopoMaps();

    List<AerialPhoto> GetAerialPhotos();

    List<OtherMap> GetOtherMaps();

    List<YearlyArchiveBox> GetYearlyArchiveBoxesWithContents();

    /// <summary>按物理位置编号查找在用的年度档案盒。</summary>
    YearlyArchiveBox? FindInUseYearlyArchiveBoxByLocationCode(string boxLocationCode);

    /// <summary>按物理位置编号加载年度档案盒内资料子项及份数快照。</summary>
    List<YearlyArchiveBox> GetYearlyArchiveBoxesByIds(IReadOnlyCollection<int> boxIds);

    List<YearlyArchiveBoxMediaItemRow> GetYearlyArchiveBoxMediaItemRows(YearlyArchiveBox box);

    /// <summary>按物理位置编号查找在用的年度电子介质袋。</summary>
    YearlyElectronicArchiveUnit? FindInUseElectronicArchiveUnitByLocationCode(string storageLocationCode);

    /// <summary>按 Id 查找在用的年度电子介质袋。</summary>
    YearlyElectronicArchiveUnit? FindInUseElectronicArchiveUnitById(int unitId);

    /// <summary>加载电子介质袋内资料子项及库存快照。</summary>
    List<YearlyArchiveBoxMediaItemRow> GetElectronicArchiveUnitMediaItemRows(YearlyElectronicArchiveUnit unit);

    /// <summary>按年度档案盒 Id 汇总在途出库提档预订占用。</summary>
    Dictionary<int, CabinetOccupationLockDescriptor> GetActiveWithdrawalLocksByArchiveBoxIds(IReadOnlyCollection<int> boxIds);

    /// <summary>按电子介质袋 Id 汇总在途出库提档预订占用。</summary>
    Dictionary<int, CabinetOccupationLockDescriptor> GetActiveWithdrawalLocksByElectronicUnitIds(IReadOnlyCollection<int> unitIds);

    /// <summary>按立档事实 Id 汇总在途出库提档预订。</summary>
    IReadOnlyDictionary<int, IReadOnlyList<ActiveWithdrawalReservationSnapshot>> GetActiveWithdrawalReservationsByFilingFactIds(IReadOnlyCollection<int> filingFactIds);

    /// <summary>读取电子介质袋关联硬盘的占用锁记录。</summary>
    IReadOnlyList<CabinetHardDiskOccupationLockInfo> GetHardDiskOccupationLocksByElectronicUnitId(int electronicArchiveUnitId);

    /// <summary>按硬盘介质 Id 汇总在途出库申请占用（含永久出库，弥补历史未写入征用锁的记录）。</summary>
    Dictionary<int, CabinetOccupationLockDescriptor> GetActiveOutboundApplicationLocksByMediumIds(IReadOnlyCollection<int> mediumIds);

    /// <summary>读取年度模拟档案盒内待还资料追溯明细（已办结出库、尚未归还的提档记录）。</summary>
    IReadOnlyList<SimulatedArchiveBoxPendingReturnDetailRow> GetSimulatedArchiveBoxPendingReturnDetails(string boxLocationCode);
}
