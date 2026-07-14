namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 硬盘介质与电子立档单元的关联信息。
    /// </summary>
    /// <param name="HardDiskMediumId">硬盘介质主键。</param>
    /// <param name="DiskCode">硬盘编号。</param>
    /// <param name="ElectronicArchiveUnitId">电子立档单元主键。</param>
    /// <param name="ElectronicArchiveNo">电子立档编号。</param>
    public sealed record HardDiskElectronicArchiveLinkInfo(
        int HardDiskMediumId,
        string DiskCode,
        int ElectronicArchiveUnitId,
        string ElectronicArchiveNo);
}
