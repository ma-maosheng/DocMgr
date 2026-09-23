using DocMgr.Models.SystemSettings;
using DocMgr.Models.YearlyArchive;
using DocMgr.Services.SystemSettings;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还：审核审批签字行装配（签批交接单复用）。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private const string BlankApprovalDateText = "______年___月___日";

        /// <summary>
        /// 归还单审批签字行：按签批链启用节点输出；优先归还单已录入值，否则回退出库单。
        /// </summary>
        private static List<ArchiveReturnApprovalSignatureLine> BuildReturnApprovalLines(
            ApprovalChainResolution chain,
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound,
            bool blankApprovalSignatures,
            string roleLabelPrefix = "借出时")
        {
            ArgumentNullException.ThrowIfNull(chain);
            ArgumentNullException.ThrowIfNull(record);

            var lines = new List<ArchiveReturnApprovalSignatureLine>();
            foreach (var signer in chain.EnabledSigners())
            {
                string roleLabel = roleLabelPrefix + signer.DisplayName;
                if (blankApprovalSignatures)
                {
                    lines.Add(CreateBlankApprovalLine(roleLabel));
                    continue;
                }

                string name = ResolveReturnSignerName(signer.NodeKey, record, outbound, signer.DefaultRealName);
                DateTime? date = ResolveReturnSignerDate(signer.NodeKey, record, outbound);
                lines.Add(CreateFilledApprovalLine(roleLabel, name, date));
            }

            return lines;
        }

        private static string ResolveReturnSignerName(
            string nodeKey,
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound,
            string chainDefault)
        {
            string fromRecord = ApprovalChainApplySupport.ReadReturnSigner(record, nodeKey)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromRecord))
            {
                return fromRecord;
            }

            string? raw = nodeKey switch
            {
                ApprovalWorkflowDomainValues.NodeDeptHead => outbound?.DeptHead,
                ApprovalWorkflowDomainValues.NodeArchiveRoomHead => outbound?.ArchiveRoomHead,
                ApprovalWorkflowDomainValues.NodeProductionHead => outbound?.ProductionHead,
                ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident => outbound?.ArchiveDeputyPresident,
                ApprovalWorkflowDomainValues.NodeProductionVicePresident => outbound?.ProductionVicePresident,
                _ => null
            };
            string fromOutbound = raw?.Trim() ?? string.Empty;

            return !string.IsNullOrWhiteSpace(fromOutbound)
                ? fromOutbound
                : (chainDefault?.Trim() ?? string.Empty);
        }

        private static DateTime? ResolveReturnSignerDate(
            string nodeKey,
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound) =>
            nodeKey switch
            {
                ApprovalWorkflowDomainValues.NodeDeptHead => record.DeptHeadDate ?? outbound?.DeptHeadDate,
                ApprovalWorkflowDomainValues.NodeArchiveRoomHead => record.ArchiveRoomHeadDate ?? outbound?.ArchiveRoomHeadDate,
                ApprovalWorkflowDomainValues.NodeProductionHead => record.ProductionHeadDate ?? outbound?.ProductionHeadDate,
                ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident => record.ArchiveDeputyPresidentDate ?? outbound?.ArchiveDeputyPresidentDate,
                ApprovalWorkflowDomainValues.NodeProductionVicePresident => record.ProductionVicePresidentDate ?? outbound?.ProductionVicePresidentDate,
                _ => null
            };

        private static ArchiveReturnApprovalSignatureLine CreateBlankApprovalLine(string roleLabel) =>
            new()
            {
                RoleLabel = roleLabel,
                SignerSlot = string.Empty,
                DateText = BlankApprovalDateText
            };

        private static ArchiveReturnApprovalSignatureLine CreateFilledApprovalLine(
            string roleLabel,
            string? signer,
            DateTime? date) =>
            new()
            {
                RoleLabel = roleLabel,
                SignerSlot = signer?.Trim() ?? string.Empty,
                DateText = date.HasValue ? date.Value.ToString("yyyy-MM-dd") : BlankApprovalDateText
            };
    }
}
