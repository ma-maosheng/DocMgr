using DocMgr.Models.YearlyArchive;

namespace DocMgr.Services.YearlyArchive
{
    /// <summary>
    /// 资料归还：审核审批签字行装配（签批交接单复用）。
    /// </summary>
    public sealed partial class ArchiveReturnService
    {
        private const string BlankApprovalDateText = "______年___月___日";

        /// <summary>
        /// 归还单审批签字行：优先归还单已录入值，否则回退出库单借出时签字。
        /// </summary>
        private static List<ArchiveReturnApprovalSignatureLine> BuildReturnApprovalLines(
            YearlyArchiveReturnRecord record,
            YearlyArchiveOutboundRecord? outbound,
            bool blankApprovalSignatures)
        {
            if (blankApprovalSignatures)
            {
                return
                [
                    CreateBlankApprovalLine("借出时部门负责人"),
                    CreateBlankApprovalLine("借出时资料室负责人"),
                    CreateBlankApprovalLine("借出时生产科负责人"),
                    CreateBlankApprovalLine("借出时生产副院长")
                ];
            }

            return
            [
                CreateFilledApprovalLine(
                    "借出时部门负责人",
                    FirstNonEmptySigner(record.ReviewerName, outbound?.DeptAuditor),
                    record.ReviewerDate ?? outbound?.DeptAuditDate),
                CreateFilledApprovalLine(
                    "借出时资料室负责人",
                    FirstNonEmptySigner(record.ApprovedBy, outbound?.ArchiveRoomHead),
                    record.ApprovedAt ?? outbound?.ArchiveRoomHeadDate),
                CreateFilledApprovalLine(
                    "借出时生产科负责人",
                    FirstNonEmptySigner(record.ProductionHead, outbound?.ProductionHead),
                    record.ProductionHeadDate ?? outbound?.ProductionHeadDate),
                CreateFilledApprovalLine(
                    "借出时生产副院长",
                    FirstNonEmptySigner(record.VicePresident, outbound?.VicePresident),
                    record.VicePresidentDate ?? outbound?.VicePresidentDate)
            ];
        }

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

        private static string FirstNonEmptySigner(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }
}
