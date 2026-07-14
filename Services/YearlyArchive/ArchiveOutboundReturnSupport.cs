using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料借出归还日期与超期判定辅助逻辑。
    /// </summary>
    public static class ArchiveOutboundReturnSupport
    {
        /// <summary>
        /// 明细是否需填写预计归还日期。
        /// </summary>
        public static bool ItemRequiresExpectedReturnDate(YearlyArchiveOutboundItem item) =>
            (string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
             && item.RequisitionedDiskNeedReturn)
            || (string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
                && item.NeedReturn);

        /// <summary>
        /// 解析库内空盘征用后是否需归还。
        /// </summary>
        public static bool ResolveRequisitionedDiskNeedReturn(YearlyArchiveOutboundItem item) =>
            item.UsageMode == ArchiveOutboundDomainValues.UsageModeDuplicate
                ? item.RequisitionedDiskNeedReturn
                : item.NeedReturn;

        /// <summary>
        /// 判断资料出库明细是否为库内空盘征用且需归还。
        /// </summary>
        public static bool IsArchiveOutboundRequisitionReturnItem(YearlyArchiveOutboundItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            if (item.RequisitionedMediumId is not > 0)
            {
                return false;
            }

            if (!string.Equals(
                    item.ElectronicMediaSource,
                    ArchiveOutboundDomainValues.ElectronicMediaSourceInStockBlank,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return ResolveRequisitionedDiskNeedReturn(item);
        }

        /// <summary>
        /// 解析明细有效应还日期（优先明细，其次申请单头）。
        /// </summary>
        public static DateTime? ResolveItemExpectedReturnDate(
            YearlyArchiveOutboundItem item,
            YearlyArchiveOutboundRecord? record = null) =>
            item.ExpectedReturnDate ?? record?.ExpectedReturnDate;

        /// <summary>
        /// 按盒/袋领用设置判断是否需要预计归还日期。
        /// </summary>
        public static bool UnitRequiresExpectedReturnDate(
            string usageMode,
            bool needReturn,
            bool useInStockBlankDisk,
            bool requisitionedDiskNeedReturn,
            bool isElectronicMedia,
            bool isHardDiskCarrier) =>
            (string.Equals(usageMode, ArchiveOutboundDomainValues.UsageModeDuplicate, StringComparison.Ordinal)
             && useInStockBlankDisk
             && requisitionedDiskNeedReturn)
            || (string.Equals(usageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
                && needReturn
                && (!isElectronicMedia || isHardDiskCarrier));

        /// <summary>
        /// 将申请单头预计归还日期同步为各需归还明细中的最早日期。
        /// </summary>
        public static void SyncRecordExpectedReturnDate(
            YearlyArchiveOutboundRecord record,
            IReadOnlyList<YearlyArchiveOutboundItem> items)
        {
            ArgumentNullException.ThrowIfNull(record);

            var dueDates = items
                .Where(ItemRequiresExpectedReturnDate)
                .Select(item => item.ExpectedReturnDate)
                .Where(date => date.HasValue)
                .Select(date => date!.Value.Date)
                .OrderBy(date => date)
                .ToList();

            record.ExpectedReturnDate = dueDates.Count > 0 ? dueDates[0] : null;
        }

        /// <summary>
        /// 已完成出库单是否存在逾期未归还的提档项。
        /// </summary>
        public static bool HasOverdueWithdrawalItems(
            YearlyArchiveOutboundRecord record,
            DateTime asOf)
        {
            ArgumentNullException.ThrowIfNull(record);

            return record.Items.Any(item =>
                string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
                && item.NeedReturn
                && !string.Equals(item.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseReturned, StringComparison.Ordinal)
                && IsDueDateOverdue(ResolveItemExpectedReturnDate(item, record), asOf));
        }

        /// <summary>
        /// 申请单是否已超过预计归还日期（用于列表高亮）。
        /// </summary>
        public static bool IsOutboundOverdue(YearlyArchiveOutboundRecord record, DateTime asOf) =>
            HasOverdueWithdrawalItems(record, asOf)
            || HasOverdueDiskRequisitionItems(record, asOf, borrowedMediumIds: null);

        /// <summary>
        /// 已完成出库单是否存在逾期未归还的库内征用硬盘。
        /// </summary>
        public static bool HasOverdueDiskRequisitionItems(
            YearlyArchiveOutboundRecord record,
            DateTime asOf,
            IReadOnlySet<int>? borrowedMediumIds)
        {
            ArgumentNullException.ThrowIfNull(record);

            return record.Items.Any(item =>
                item.RequisitionedDiskNeedReturn
                && item.RequisitionedMediumId is > 0
                && IsDueDateOverdue(ResolveItemExpectedReturnDate(item, record), asOf)
                && (borrowedMediumIds == null
                    || borrowedMediumIds.Contains(item.RequisitionedMediumId.Value)));
        }

        public static bool IsDueDateOverdue(DateTime? dueDate, DateTime asOf) =>
            dueDate.HasValue && dueDate.Value.Date < asOf.Date;
    }
}
