using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.SystemSettings
{
    /// <summary>按签批链生成打印签字行（一级一行语义由调用方决定是否分组）。</summary>
    public static class ApprovalChainSignatureLineSupport
    {
        public sealed class SignatureLine
        {
            public required string RoleLabel { get; init; }

            public string SignerName { get; init; } = string.Empty;

            public string NodeKey { get; init; } = string.Empty;
        }

        /// <summary>
        /// 生成启用节点的签字行。blank=true 时姓名留白；否则优先 currentName，再回退链默认名。
        /// </summary>
        public static IReadOnlyList<SignatureLine> BuildEnabledLines(
            ApprovalChainResolution chain,
            Func<string, string?>? readCurrentName = null,
            bool blank = false,
            string roleLabelPrefix = "")
        {
            ArgumentNullException.ThrowIfNull(chain);

            var lines = new List<SignatureLine>();
            foreach (var signer in chain.EnabledSigners())
            {
                string current = readCurrentName?.Invoke(signer.NodeKey)?.Trim() ?? string.Empty;
                string name = blank
                    ? string.Empty
                    : (!string.IsNullOrWhiteSpace(current) ? current : signer.DefaultRealName);

                lines.Add(new SignatureLine
                {
                    NodeKey = signer.NodeKey,
                    RoleLabel = roleLabelPrefix + signer.DisplayName,
                    SignerName = name
                });
            }

            return lines;
        }

        /// <summary>按「一级一行」折叠为打印段落文本（未启用级别整段省略；签字格式走 <see cref="PrintApprovalSignatureSupport"/>）。</summary>
        public static string BuildLevelGroupedText(
            ApprovalChainResolution chain,
            Func<string, string?>? readCurrentName = null,
            bool blank = false,
            string? blankDateSuffix = null)
        {
            _ = blankDateSuffix; // 保留参数兼容旧调用；日期统一用 PrintApprovalSignatureSupport 留白。

            var rows = ApprovalChainPrintLayoutSupport.BuildRows(chain);
            if (rows.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            foreach (var row in rows)
            {
                var lineParts = new List<string>();
                foreach (var (label, defaultName) in row.Signers)
                {
                    string nodeKey = ResolveNodeKey(label);
                    string current = readCurrentName?.Invoke(nodeKey)?.Trim() ?? string.Empty;
                    string name = blank
                        ? string.Empty
                        : (!string.IsNullOrWhiteSpace(current)
                            ? current
                            : defaultName);

                    lineParts.Add(PrintApprovalSignatureSupport.FormatLabeledInline(
                        label + "签字",
                        string.IsNullOrWhiteSpace(name) ? null : name,
                        dateText: null));
                }

                if (lineParts.Count > 0)
                {
                    parts.Add(string.Join("\n", lineParts));
                }
            }

            return string.Join("\n", parts);
        }

        private static string ResolveNodeKey(string displayName) => displayName switch
        {
            ApprovalWorkflowDomainValues.DisplayDeptHead => ApprovalWorkflowDomainValues.NodeDeptHead,
            ApprovalWorkflowDomainValues.DisplayArchiveRoomHead => ApprovalWorkflowDomainValues.NodeArchiveRoomHead,
            "开发室签字" => ApprovalWorkflowDomainValues.NodeArchiveRoomHead,
            ApprovalWorkflowDomainValues.DisplayProductionHead => ApprovalWorkflowDomainValues.NodeProductionHead,
            ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident => ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident,
            ApprovalWorkflowDomainValues.DisplayProductionVicePresident => ApprovalWorkflowDomainValues.NodeProductionVicePresident,
            // 兼容旧标签
            "部门负责人" => ApprovalWorkflowDomainValues.NodeDeptHead,
            "资料室负责人" => ApprovalWorkflowDomainValues.NodeArchiveRoomHead,
            "生产管理科负责人" => ApprovalWorkflowDomainValues.NodeProductionHead,
            "分管资料副院长" => ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident,
            "分管生产副院长" => ApprovalWorkflowDomainValues.NodeProductionVicePresident,
            _ => string.Empty
        };
    }
}
