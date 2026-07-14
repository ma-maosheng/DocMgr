using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 同一借出申请内多块库内硬盘（拷贝）共用时，归还设置一致性辅助逻辑。
    /// </summary>
    public static class ArchiveOutboundSharedDiskSettingsSupport
    {
        /// <summary>
        /// 明细是否属于「拷贝 + 库内空盘」且已选定硬盘。
        /// </summary>
        public static bool UsesInStockBlankDisk(YearlyArchiveOutboundItem item) =>
            string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
            && string.Equals(
                item.ElectronicMediaSource,
                ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank,
                StringComparison.Ordinal)
            && item.RequisitionedMediumId is > 0;

        /// <summary>
        /// 主控介质袋：多袋共用同一库内硬盘时的界面提示。
        /// </summary>
        public static string BuildPrimarySharedDiskHint(int bagCount, string? diskCode)
        {
            string diskPart = FormatDiskLabel(diskCode);
            return $"有{bagCount}个介质袋内的资料将共用一块{diskPart}来完成资料拷贝";
        }

        /// <summary>
        /// 从属介质袋：共用硬盘且归还设置由主控袋同步时的界面提示。
        /// </summary>
        public static string BuildPeerSharedDiskHint(int bagCount, string? diskCode, string primaryUnitTitle)
        {
            string title = string.IsNullOrWhiteSpace(primaryUnitTitle) ? "其他介质袋" : primaryUnitTitle.Trim();
            return $"{BuildPrimarySharedDiskHint(bagCount, diskCode)}；本袋归还设置已与「{title}」同步。";
        }

        /// <summary>
        /// 跨介质袋校验：同一 <see cref="YearlyArchiveOutboundItem.RequisitionedMediumId"/> 的归还设置须一致。
        /// </summary>
        public static List<string> ValidateCrossUnitConsistency(IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            var errors = new List<string>();

            foreach (var group in items
                         .Where(UsesInStockBlankDisk)
                         .GroupBy(item => item.RequisitionedMediumId!.Value))
            {
                var groupItems = group.ToList();
                if (groupItems.Count <= 1)
                {
                    continue;
                }

                var sample = groupItems[0];
                string diskCode = sample.RequisitionedDiskCode?.Trim() ?? string.Empty;
                string diskLabel = string.IsNullOrWhiteSpace(diskCode)
                    ? $"ID={group.Key}"
                    : diskCode;

                if (groupItems.Any(item => item.RequisitionedDiskNeedReturn != sample.RequisitionedDiskNeedReturn))
                {
                    errors.Add($"• 硬盘 [{diskLabel}]：多个介质袋共用时，硬盘是否归还须一致。");
                }

                if (sample.RequisitionedDiskNeedReturn
                    && groupItems.Any(item => item.ExpectedReturnDate != sample.ExpectedReturnDate))
                {
                    errors.Add($"• 硬盘 [{diskLabel}]：多个介质袋共用时，预计归还日期须一致。");
                }
            }

            return errors;
        }

        private static string FormatDiskLabel(string? diskCode)
        {
            string code = diskCode?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(code) ? "库内硬盘" : $"硬盘[{code}]";
        }
    }
}
