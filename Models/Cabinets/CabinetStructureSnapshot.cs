namespace DocMgr.Models.Cabinets;

/// <summary>
/// 数据库中柜体当前的结构（编号、种类、面/层/列），用于编辑保存前对照。
/// </summary>
public sealed record CabinetStructureSnapshot(
    int Id,
    string Name,
    CabinetType Type,
    int FaceCount,
    int LayerCount,
    int ColumnCount,
    int HardDiskSlotCapacity,
    int OpticalDiscSlotCapacity);
