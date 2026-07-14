using DocMgr.Data;
using DocMgr.Models.Cabinets;
using DocMgr.Models.YearlyArchive;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.Cabinets;

public class CabinetArchiveBoxPlacementRepository : ICabinetArchiveBoxPlacementRepository
{
    private readonly AppDbContext _dbContext;

    public CabinetArchiveBoxPlacementRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public CabinetArchiveBoxPlacement? GetPlacementByBoxCode(string boxCode)
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .FirstOrDefault(item => item.BoxCode == boxCode);
    }

    public List<CabinetArchiveBoxPlacement> GetPlacementsBySlot(string cabinetName, string faceCode, string slotCode)
    {
        return _dbContext.CabinetArchiveBoxPlacements
            .Where(item => item.CabinetName == cabinetName)
            .Where(item => item.FaceCode == faceCode)
            .Where(item => item.SlotCode == slotCode)
            .ToList();
    }

    public void AddPlacement(CabinetArchiveBoxPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        _dbContext.CabinetArchiveBoxPlacements.Add(placement);
    }

    public List<YearlyArchiveBox> GetYearlyArchiveBoxesByLocationCodes(IReadOnlyCollection<string> boxCodes)
    {
        return _dbContext.YearlyArchiveBoxes
            .Where(item => boxCodes.Contains(item.BoxLocationCode))
            .ToList();
    }

    public YearlyArchiveBox? GetYearlyArchiveBoxByLocationCode(string boxCode)
    {
        return _dbContext.YearlyArchiveBoxes
            .FirstOrDefault(item => item.BoxLocationCode == boxCode);
    }

    public List<ArchiveBoxSpecification> GetArchiveBoxSpecifications()
    {
        return _dbContext.ArchiveBoxSpecifications
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
