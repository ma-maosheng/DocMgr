using DocMgr.Models.Cabinets;
using DocMgr.Models.HistoryArchive;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 档案盒摆放数据访问契约：直接读写历史/年度档案盒实体的放置方式与规格。
/// </summary>
public interface ICabinetArchiveBoxPlacementRepository
{
    /// <summary>按物理位置编号查找在用年度档案盒。</summary>
    YearlyArchiveBox? GetYearlyArchiveBoxByLocationCode(string boxLocationCode);

    /// <summary>按物理位置编号集合批量查找在用年度档案盒。</summary>
    List<YearlyArchiveBox> GetYearlyArchiveBoxesByLocationCodes(IReadOnlyCollection<string> boxLocationCodes);

    /// <summary>查找指定柜体、面别、档口下全部在用年度档案盒。</summary>
    List<YearlyArchiveBox> GetInUseYearlyArchiveBoxesBySlot(string cabinetName, string faceCode, string slotCode);

    /// <summary>按盒号查找在库历史档案盒。</summary>
    HistoryArchiveBox? GetHistoryArchiveBoxByCode(string boxCode);

    /// <summary>查找指定柜体、面别、档口下全部在库历史档案盒。</summary>
    List<HistoryArchiveBox> GetInStockHistoryArchiveBoxesBySlot(string cabinetName, string faceCode, string slotCode);

    /// <summary>历史档案盒放置方式查找表（盒号 → 放置方式）。</summary>
    Dictionary<string, string> GetHistoryPlacementModeLookup(string cabinetName);

    /// <summary>年度档案盒放置方式查找表（位置编号 → 放置方式）。</summary>
    Dictionary<string, string> GetYearlyPlacementModeLookup(string cabinetName);

    /// <summary>档案盒规格字典表全量。</summary>
    List<ArchiveBoxSpecification> GetArchiveBoxSpecifications();

    int SaveChanges();
}
