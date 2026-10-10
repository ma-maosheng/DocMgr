namespace DocMgr.ViewModels.YearlyArchive;

/// <summary>
/// 资料室存档出网可选的并档硬盘袋。
/// </summary>
public sealed class ArchiveFilingAppendTargetOption
{
    public int UnitId { get; init; }

    public string DiskCode { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public static ArchiveFilingAppendTargetOption From(YearlyElectronicArchiveUnit unit)
    {
        string diskCode = unit.LinkedMediumCodes?.Trim() ?? string.Empty;
        string bagNo = unit.ElectronicArchiveNo?.Trim() ?? string.Empty;
        string location = unit.StorageLocation?.Trim() ?? string.Empty;
        return new ArchiveFilingAppendTargetOption
        {
            UnitId = unit.Id,
            DiskCode = diskCode,
            DisplayText = string.Join(" / ", new[] { bagNo, diskCode, location }.Where(part => !string.IsNullOrWhiteSpace(part)))
        };
    }
}
