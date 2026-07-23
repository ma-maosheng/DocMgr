namespace DocMgr.Services.HardDiskMedia;

/// <summary>
/// 盘库登记明细草稿（含目标档口）。
/// </summary>
public sealed class HardDiskInventoryRegisterItemDraft
{
    public int MediumId { get; set; }

    public string TargetStorageLocation { get; set; } = string.Empty;
}
