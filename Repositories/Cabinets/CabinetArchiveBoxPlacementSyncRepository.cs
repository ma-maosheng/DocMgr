using DocMgr.Data;
using DocMgr.Repositories.Interfaces;

namespace DocMgr.Repositories.Cabinets;

public class CabinetArchiveBoxPlacementSyncRepository : ICabinetArchiveBoxPlacementSyncRepository
{
    private readonly AppDbContext _dbContext;

    public CabinetArchiveBoxPlacementSyncRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public List<CabinetArchiveBoxPlacement> GetPlacements()
    {
        return _dbContext.CabinetArchiveBoxPlacements.ToList();
    }

    public void AddPlacement(CabinetArchiveBoxPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        _dbContext.CabinetArchiveBoxPlacements.Add(placement);
    }

    public List<TopoMap> GetTopoMaps()
    {
        return _dbContext.TopoMaps.ToList();
    }

    public List<AerialPhoto> GetAerialPhotos()
    {
        return _dbContext.AerialPhotos.ToList();
    }

    public List<OtherMap> GetOtherMaps()
    {
        return _dbContext.OtherMaps.ToList();
    }

    public int SaveChanges()
    {
        return _dbContext.SaveChanges();
    }
}
