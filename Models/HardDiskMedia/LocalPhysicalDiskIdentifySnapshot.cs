namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 通过 ATA/NVMe Identify 读到的盘体身份；失败时不使用本快照。
    /// </summary>
    public sealed class LocalPhysicalDiskIdentifySnapshot
    {
        /// <summary>盘体序列号。</summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>盘体型号。</summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>固件版本。</summary>
        public string Firmware { get; init; } = string.Empty;

        /// <summary>盘体总线名（SATA / NVMe），不是 USB 桥。</summary>
        public string BusTypeName { get; init; } = string.Empty;

        /// <summary>由 Identify 判定的硬盘类型；无法判定时为空。</summary>
        public string DiskType { get; init; } = string.Empty;
    }
}
