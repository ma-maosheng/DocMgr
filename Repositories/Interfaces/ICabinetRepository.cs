using DocMgr.Models.Cabinets;

namespace DocMgr.Repositories.Interfaces;

/// <summary>
/// 档案柜数据访问契约：柜体与档口数据读写。
/// </summary>
public interface ICabinetRepository
{
    List<Cabinet> GetAll();

    Task<List<Cabinet>> GetAllAsync();

    bool Any();

    Task<bool> AnyAsync();

    Cabinet? GetById(int cabinetId);

    void Add(Cabinet cabinet);

    void AddRange(IEnumerable<Cabinet> cabinets);

    void Update(Cabinet cabinet);

    void Remove(Cabinet cabinet);

    CabinetHardDiskSlotCategoryAssignment? GetSlotCategoryAssignment(int cabinetId, string faceCode, string slotCode);

    List<CabinetHardDiskSlotCategoryAssignment> GetSlotCategoryAssignmentsByCabinetId(int cabinetId);

    void AddSlotCategoryAssignment(CabinetHardDiskSlotCategoryAssignment assignment);

    void RemoveSlotCategoryAssignment(CabinetHardDiskSlotCategoryAssignment assignment);

    /// <summary>
    /// 判断防磁磁盘柜指定档口是否仍有在库硬盘或光盘占用。
    /// </summary>
    bool HasInStockMediaInMagneticDiskSlot(string cabinetName, string faceCode, string slotCode);

    int SaveChanges();

    Task<int> SaveChangesAsync();
}
