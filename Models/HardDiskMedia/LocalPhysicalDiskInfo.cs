namespace DocMgr.Models.HardDiskMedia
{
    /// <summary>
    /// 本机物理磁盘硬件信息，供硬盘介质半自动登记回填。
    /// </summary>
    public sealed class LocalPhysicalDiskInfo
    {
        /// <summary>Windows 物理盘序号（PHYSICALDRIVE n）。</summary>
        public int DiskIndex { get; init; }

        /// <summary>WMI 设备标识，如 \\.\PHYSICALDRIVE1。</summary>
        public string DeviceId { get; init; } = string.Empty;

        /// <summary>硬件型号原文。</summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>规范化后的序列号；硬件未提供时为空。</summary>
        public string SerialNumber { get; init; } = string.Empty;

        /// <summary>从型号解析出的品牌。</summary>
        public string Brand { get; init; } = string.Empty;

        /// <summary>容量数值（与 <see cref="CapacityUnit"/> 配套）。</summary>
        public string CapacityValue { get; init; } = string.Empty;

        /// <summary>容量单位（GB/TB）。</summary>
        public string CapacityUnit { get; init; } = string.Empty;

        /// <summary>容量展示文本。</summary>
        public string CapacityText { get; init; } = string.Empty;

        /// <summary>映射后的硬盘类型域值。</summary>
        public string DiskType { get; init; } = string.Empty;

        /// <summary>映射后的接口类型域值。</summary>
        public string InterfaceType { get; init; } = string.Empty;

        /// <summary>已分配盘符，多个以顿号分隔。</summary>
        public string DriveLetters { get; init; } = string.Empty;

        /// <summary>从制造日期或制造年份解析出的出厂日期；无法解析时为空。</summary>
        public DateTime? FactoryDate { get; init; }

        /// <summary>选盘列表中的制造年展示。</summary>
        public string ManufactureYearText => FactoryDate?.ToString("yyyy") ?? string.Empty;

        /// <summary>是否为安装操作系统的物理盘。</summary>
        public bool IsSystemDisk { get; init; }

        /// <summary>是否为文件虚拟磁盘。</summary>
        public bool IsVirtualDisk { get; init; }

        /// <summary>是否允许作为登记候选。</summary>
        public bool CanRegister => !IsSystemDisk && !IsVirtualDisk && DiskIndex >= 0;

        /// <summary>列表说明（系统盘、缺序列号等）。</summary>
        public string StatusHint { get; init; } = string.Empty;

        /// <summary>选择列表摘要。</summary>
        public string DisplaySummary =>
            string.Join(" / ", new[]
                {
                    string.IsNullOrWhiteSpace(Model) ? $"磁盘{DiskIndex}" : Model.Trim(),
                    CapacityText,
                    string.IsNullOrWhiteSpace(SerialNumber) ? "无序列号" : SerialNumber,
                    string.IsNullOrWhiteSpace(DriveLetters) ? string.Empty : DriveLetters
                }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
