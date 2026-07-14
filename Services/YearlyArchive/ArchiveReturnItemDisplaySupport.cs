using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还明细展示字段与出库明细快照同步。
    /// </summary>
    internal static class ArchiveReturnItemDisplaySupport
    {
        public static void ApplyOutboundSnapshot(YearlyArchiveReturnItem returnItem, YearlyArchiveOutboundItem outboundItem)
        {
            ArgumentNullException.ThrowIfNull(returnItem);
            ArgumentNullException.ThrowIfNull(outboundItem);

            returnItem.MediaType = outboundItem.MediaType?.Trim() ?? string.Empty;
            returnItem.StorageCarrierType = ResolveStorageCarrierType(outboundItem);
            returnItem.ItemArchiveYear = outboundItem.ItemArchiveYear;
            returnItem.ItemProjectName = outboundItem.ItemProjectName?.Trim() ?? string.Empty;
            returnItem.ConfidentialLevel = outboundItem.ConfidentialLevel?.Trim() ?? string.Empty;
            returnItem.SelectionScopeDisplay = outboundItem.SelectionScopeDisplay?.Trim() ?? string.Empty;
            returnItem.DiskInfo = BuildDiskInfo(outboundItem);
        }

        public static void EnrichFromOutbound(YearlyArchiveReturnRecord record, YearlyArchiveOutboundRecord? outbound)
        {
            if (outbound == null || record.Items.Count == 0)
            {
                return;
            }

            foreach (var returnItem in record.Items)
            {
                var outboundItem = outbound.Items.FirstOrDefault(item => item.Id == returnItem.SourceOutboundItemId);
                if (outboundItem != null)
                {
                    ApplyOutboundSnapshot(returnItem, outboundItem);
                }
            }
        }

        public static bool IsReturnableOutboundItem(YearlyArchiveOutboundItem item) =>
            string.Equals(item.MediaKind, ArchiveRegisterDomainValues.MediaKindSimulated, StringComparison.Ordinal)
            && string.Equals(item.UsageMode, ArchiveOutboundDomainValues.UsageModeWithdrawal, StringComparison.Ordinal)
            && item.NeedReturn
            && !string.Equals(item.ReservationStatus, ArchiveOutboundDomainValues.SyncEntryPhaseReturned, StringComparison.Ordinal);

        private static string BuildDiskInfo(YearlyArchiveOutboundItem item)
        {
            if (!string.IsNullOrWhiteSpace(item.RequisitionedDiskCode))
            {
                string clause = item.RequisitionedDiskCode.Trim();
                if (item.ShowRequisitionedDiskNeedReturn)
                {
                    clause += item.RequisitionedDiskNeedReturn ? "（需归还）" : "（不需归还）";
                }

                return clause;
            }

            if (!string.IsNullOrWhiteSpace(item.SelfDiskSerialNo))
            {
                return item.SelfDiskSerialNo.Trim();
            }

            return string.Empty;
        }

        private static string ResolveStorageCarrierType(YearlyArchiveOutboundItem outboundItem)
        {
            if (!string.IsNullOrWhiteSpace(outboundItem.StorageCarrierType))
            {
                return outboundItem.StorageCarrierType.Trim();
            }

            return outboundItem.MediaType?.Trim() ?? string.Empty;
        }
    }
}
