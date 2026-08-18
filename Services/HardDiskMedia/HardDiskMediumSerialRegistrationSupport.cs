namespace DocMgr.Services.HardDiskMedia
{
    /// <summary>
    /// 按序列号核验硬盘是否已登记，避免重复登记。
    /// </summary>
    public static class HardDiskMediumSerialRegistrationSupport
    {
        /// <summary>
        /// 组装已登记序列号的提示文本。
        /// </summary>
        public static string BuildAlreadyRegisteredMessage(HardDiskMedium existing)
        {
            ArgumentNullException.ThrowIfNull(existing);

            string serialNumber = existing.SerialNumber?.Trim() ?? string.Empty;
            string diskCode = string.IsNullOrWhiteSpace(existing.DiskCode) ? "（无编号）" : existing.DiskCode.Trim();
            string brand = string.IsNullOrWhiteSpace(existing.Brand) ? "—" : existing.Brand.Trim();
            string capacity = string.IsNullOrWhiteSpace(existing.Capacity) ? "—" : existing.Capacity.Trim();
            string interfaceType = string.IsNullOrWhiteSpace(existing.InterfaceType) ? "—" : existing.InterfaceType.Trim();
            string status = string.IsNullOrWhiteSpace(existing.Ledger?.MediaStatus)
                ? "（无台账状态）"
                : existing.Ledger!.MediaStatus.Trim();
            string location = string.IsNullOrWhiteSpace(existing.Ledger?.StorageLocation)
                ? "（未登记存放位置）"
                : existing.Ledger!.StorageLocation.Trim();

            return $"序列号 [{serialNumber}] 已登记，禁止重复登记。"
                + $"{Environment.NewLine}硬盘编号：{diskCode}"
                + $"{Environment.NewLine}品牌/容量/接口：{brand} / {capacity} / {interfaceType}"
                + $"{Environment.NewLine}介质状态：{status}"
                + $"{Environment.NewLine}存放位置：{location}";
        }
    }
}
