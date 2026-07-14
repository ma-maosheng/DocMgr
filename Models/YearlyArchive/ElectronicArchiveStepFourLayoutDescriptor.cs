namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 电子介质立档第4至第6步界面布局：根据场景显示外来硬盘登记、格式化后空盘入库位置和库内空白硬盘选择。
    /// </summary>
    public sealed record ElectronicArchiveStepFourLayoutDescriptor(
        bool ShowExternalHardDiskRegistration,
        bool ShowExternalHardDiskFormattedBlankLocation,
        bool ShowBlankInventoryHardDiskSelection);
}
