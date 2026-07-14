using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 档案盒摆放数据访问契约：档案盒在档口内的摆放记录读写。
/// </summary>
public interface ICabinetArchiveBoxPlacementRepository
{
    CabinetArchiveBoxPlacement? GetPlacementByBoxCode(string boxCode);

    List<CabinetArchiveBoxPlacement> GetPlacementsBySlot(string cabinetName, string faceCode, string slotCode);

    void AddPlacement(CabinetArchiveBoxPlacement placement);

    List<YearlyArchiveBox> GetYearlyArchiveBoxesByLocationCodes(IReadOnlyCollection<string> boxCodes);

    YearlyArchiveBox? GetYearlyArchiveBoxByLocationCode(string boxCode);

    List<ArchiveBoxSpecification> GetArchiveBoxSpecifications();

    int SaveChanges();
}
