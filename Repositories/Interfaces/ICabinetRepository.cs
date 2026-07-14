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

    int SaveChanges();

    Task<int> SaveChangesAsync();
}
