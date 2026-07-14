namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 档案盒摆放台账同步数据访问契约：摆放结果与台账之间的同步读写。
/// </summary>
public interface ICabinetArchiveBoxPlacementSyncRepository
{
    List<CabinetArchiveBoxPlacement> GetPlacements();

    void AddPlacement(CabinetArchiveBoxPlacement placement);

    List<TopoMap> GetTopoMaps();

    List<AerialPhoto> GetAerialPhotos();

    List<OtherMap> GetOtherMaps();

    int SaveChanges();
}
