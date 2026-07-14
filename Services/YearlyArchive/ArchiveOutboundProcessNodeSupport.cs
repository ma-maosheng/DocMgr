using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 出库申请流程节点：SyncEntry 展示映射。
    /// </summary>
    internal static class ArchiveOutboundProcessNodeSupport
    {
        public static MaterialOutboundProcessNodeRow MapProcessNode(
            YearlyArchiveOutboundSyncEntry entry,
            YearlyArchiveOutboundRecord record,
            YearlyArchiveOutboundItem item)
        {
            DateTime operatedAt = entry.UpdatedAt ?? entry.CreatedAt;
            ResolveNodePresentation(entry, item, out string categoryDisplay, out string nodeDisplay, out bool isProcessOnly);

            return new MaterialOutboundProcessNodeRow
            {
                OperatedAt = operatedAt,
                OutboundNo = record.OutboundNo,
                OutboundStatusDisplay = record.StatusStr,
                NodeCategoryDisplay = categoryDisplay,
                ProcessNodeDisplay = nodeDisplay,
                UsageModeDisplay = item.UsageModeDisplay,
                ApplicantName = record.ApplicantName.Trim(),
                OperatorName = entry.OperatedBy.Trim(),
                Remark = entry.Remark?.Trim() ?? string.Empty,
                IsProcessOnly = isProcessOnly
            };
        }

        private static void ResolveNodePresentation(
            YearlyArchiveOutboundSyncEntry entry,
            YearlyArchiveOutboundItem item,
            out string categoryDisplay,
            out string nodeDisplay,
            out bool isProcessOnly)
        {
            categoryDisplay = MapNodeCategory(entry.Phase);
            nodeDisplay = MapProcessNodeDisplay(entry.EntryKind, entry.Phase, item);
            isProcessOnly = string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhaseActive, StringComparison.Ordinal)
                || string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhasePending, StringComparison.Ordinal)
                || string.Equals(entry.Phase, ArchiveOutboundDomainValues.SyncEntryPhaseCancelled, StringComparison.Ordinal);
        }

        private static string MapNodeCategory(string phase) => phase switch
        {
            ArchiveOutboundDomainValues.SyncEntryPhaseActive or ArchiveOutboundDomainValues.SyncEntryPhasePending => "流程预订",
            ArchiveOutboundDomainValues.SyncEntryPhaseCancelled => "流程撤销",
            ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed => "办结同步",
            _ => "流程节点"
        };

        private static string MapProcessNodeDisplay(
            string entryKind,
            string phase,
            YearlyArchiveOutboundItem item)
        {
            if (string.Equals(phase, ArchiveOutboundDomainValues.SyncEntryPhaseCancelled, StringComparison.Ordinal))
            {
                return entryKind switch
                {
                    ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation => "撤回申请 · 提档预订注销",
                    ArchiveOutboundDomainValues.SyncEntryKindCopyLedger => "撤回申请 · 复制预订注销",
                    ArchiveOutboundDomainValues.SyncEntryKindDuplicateLedger => "撤回申请 · 拷贝预订注销",
                    _ => "撤回申请 · 流程注销"
                };
            }

            if (string.Equals(entryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReservation, StringComparison.Ordinal))
            {
                return phase switch
                {
                    ArchiveOutboundDomainValues.SyncEntryPhaseActive => "提交申请 · 提档预订",
                    ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed => "出库办结 · 提档预订确认",
                    _ => "提档预订"
                };
            }

            if (string.Equals(entryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalLedger, StringComparison.Ordinal))
            {
                return item.NeedReturn
                    ? "资料出库办结 · 提档借出"
                    : "资料出库办结 · 提档（不需归还）";
            }

            if (string.Equals(entryKind, ArchiveOutboundDomainValues.SyncEntryKindCopyLedger, StringComparison.Ordinal))
            {
                return phase switch
                {
                    ArchiveOutboundDomainValues.SyncEntryPhasePending => "提交申请 · 复制预订",
                    ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed => "资料出库办结 · 复制借出",
                    _ => "复制出库"
                };
            }

            if (string.Equals(entryKind, ArchiveOutboundDomainValues.SyncEntryKindDuplicateLedger, StringComparison.Ordinal))
            {
                return phase switch
                {
                    ArchiveOutboundDomainValues.SyncEntryPhasePending => "提交申请 · 拷贝预订",
                    ArchiveOutboundDomainValues.SyncEntryPhaseConfirmed => "资料出库办结 · 拷贝借出",
                    _ => "拷贝出库"
                };
            }

            if (string.Equals(entryKind, ArchiveOutboundDomainValues.SyncEntryKindWithdrawalReturned, StringComparison.Ordinal))
            {
                return "资料归还 · 出库项回冲";
            }

            return $"{entryKind} · {phase}";
        }
    }
}
