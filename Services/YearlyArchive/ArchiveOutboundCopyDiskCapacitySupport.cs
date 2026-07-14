using DocMgr.Models.HardDiskMedia;
using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 借出申请「拷贝 + 库内空盘」提交前硬盘容量校验。
    /// </summary>
    public static class ArchiveOutboundCopyDiskCapacitySupport
    {
        /// <summary>
        /// 汇总单条出库明细的拟拷贝数据量（MB）。
        /// </summary>
        public static decimal ResolveCopyDataSizeMb(
            YearlyArchiveOutboundItem item,
            IReadOnlyDictionary<int, YearlyArchiveFilingFact>? filingFactsById = null)
        {
            if (item.DataSizeMb is > 0)
            {
                return item.DataSizeMb.Value;
            }

            if (filingFactsById != null
                && item.FilingFactId > 0
                && filingFactsById.TryGetValue(item.FilingFactId, out var fact)
                && fact.DataSizeMb > 0)
            {
                return fact.DataSizeMb;
            }

            return 0m;
        }

        /// <summary>
        /// 构建容量不足或未登记容量时的校验错误。
        /// </summary>
        public static string BuildInsufficientCapacityError(
            string diskLabel,
            decimal availableMb,
            decimal pendingMb,
            decimal totalMb,
            decimal usedMb)
        {
            return $"• 硬盘 [{diskLabel}]：可用容量不足，可用 {ElectronicMediaCapacitySupport.FormatCapacityMb(Math.Max(0, availableMb))}，"
                + $"本次拟拷贝资料数据量 {ElectronicMediaCapacitySupport.FormatCapacityMb(pendingMb)}。"
                + $"（总容量 {ElectronicMediaCapacitySupport.FormatCapacityMb(totalMb)}，"
                + $"已占用 {ElectronicMediaCapacitySupport.FormatCapacityMb(usedMb)}）";
        }

        public static string BuildMissingCapacityRegistrationError(string diskLabel) =>
            $"• 硬盘 [{diskLabel}]：未登记容量信息，请先在硬盘台账中补全后再提交。";

        public static string BuildMissingCopyDataSizeError(string diskLabel) =>
            $"• 硬盘 [{diskLabel}]：无法获取拟拷贝资料数据量，请完善登记数据量后再提交。";

        public static string BuildMissingMediumError(string diskLabel) =>
            $"• 硬盘 [{diskLabel}]：未找到介质台账，无法校验容量。";

        public static string FormatDiskLabel(string? diskCode) =>
            string.IsNullOrWhiteSpace(diskCode) ? "库内硬盘" : diskCode.Trim();

        public static decimal ResolveTotalCapacityMb(HardDiskMedium medium) =>
            ElectronicMediaCapacitySupport.ParseCapacityTextToMb(medium.Capacity);
    }
}
