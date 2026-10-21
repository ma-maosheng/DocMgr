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

    /// <summary>
    /// 读取柜体已落库的编号与面/层/列结构，不使用当前上下文里尚未保存的修改。
    /// </summary>
    CabinetStructureSnapshot? GetStructureSnapshot(int cabinetId);

    /// <summary>
    /// 除指定柜体外，其余柜体的编号。
    /// </summary>
    IReadOnlyList<string> GetCabinetNamesExcept(int cabinetId);

    void Add(Cabinet cabinet);

    void AddRange(IEnumerable<Cabinet> cabinets);

    void Update(Cabinet cabinet);

    void Remove(Cabinet cabinet);

    CabinetHardDiskSlotCategoryAssignment? GetSlotCategoryAssignment(int cabinetId, string faceCode, string slotCode);

    List<CabinetHardDiskSlotCategoryAssignment> GetSlotCategoryAssignmentsByCabinetId(int cabinetId);

    void AddSlotCategoryAssignment(CabinetHardDiskSlotCategoryAssignment assignment);

    void RemoveSlotCategoryAssignment(CabinetHardDiskSlotCategoryAssignment assignment);

    CabinetArchiveSlotCategoryAssignment? GetArchiveSlotCategoryAssignment(int cabinetId, string faceCode, string slotCode);

    List<CabinetArchiveSlotCategoryAssignment> GetArchiveSlotCategoryAssignmentsByCabinetId(int cabinetId);

    void AddArchiveSlotCategoryAssignment(CabinetArchiveSlotCategoryAssignment assignment);

    void RemoveArchiveSlotCategoryAssignment(CabinetArchiveSlotCategoryAssignment assignment);

    /// <summary>
    /// 判断防磁磁盘柜指定档口是否仍有在库硬盘或光盘占用。
    /// </summary>
    bool HasInStockMediaInMagneticDiskSlot(string cabinetName, string faceCode, string slotCode);

    /// <summary>
    /// 判断标准滑道式档案柜指定档口是否仍有档案盒占用。
    /// </summary>
    bool HasArchiveBoxesInStandardSlot(string cabinetName, string faceCode, string slotCode);

    /// <summary>
    /// 判断柜体内是否仍有存放内容（在用档案盒、未离库历史存档、在用电子立档单元、在库硬盘或光盘）。
    /// 档口用途配置不计入。
    /// </summary>
    bool HasStoredContent(string cabinetName);

    /// <summary>
    /// 列出柜体内仍有存放内容的档口。无内容时返回空列表。
    /// </summary>
    IReadOnlyList<CabinetStoredContentSlot> GetStoredContentSlots(string cabinetName);

    /// <summary>
    /// 统计防磁磁盘柜各档口当前在库占用的最大值（按硬盘类 / 光盘类分列）。
    /// </summary>
    (int MaxHardDiskOccupancy, int MaxOpticalDiscOccupancy) GetMaxMagneticSlotOccupancy(Cabinet cabinet);

    int SaveChanges();

    Task<int> SaveChangesAsync();
}
