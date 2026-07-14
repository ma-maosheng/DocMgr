namespace DocMgr.Models.Cabinets
{
    /// <summary>
    /// 开柜 / 内容窗体展示的硬盘占用锁摘要。
    /// </summary>
    public sealed class CabinetHardDiskOccupationLockInfo
    {
        public string DiskCode { get; init; } = string.Empty;

        public string BusinessType { get; init; } = string.Empty;

        public string BusinessNo { get; init; } = string.Empty;

        public string DisplayText =>
            string.IsNullOrWhiteSpace(DiskCode)
                ? $"业务类型：{BusinessType}；业务单号：{BusinessNo}"
                : $"硬盘 {DiskCode}：{BusinessType}（{BusinessNo}）";
    }
}
