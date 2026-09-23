namespace DocMgr.Models.SystemSettings
{
    /// <summary>单条签批节点解析结果。</summary>
    public sealed class ApprovalSignerSlot
    {
        public required string NodeKey { get; init; }

        public required string DisplayName { get; init; }

        public required bool IsEnabled { get; init; }

        public string DefaultRealName { get; init; } = string.Empty;

        public int? DefaultUserId { get; init; }
    }

    /// <summary>命中规则后的三级签批链解析结果。</summary>
    public sealed class ApprovalChainResolution
    {
        public required string BusinessType { get; init; }

        public int? MatchedRuleId { get; init; }

        public string MatchedRuleName { get; init; } = string.Empty;

        public ApprovalSignerSlot DeptHead { get; init; } = new()
        {
            NodeKey = ApprovalWorkflowDomainValues.NodeDeptHead,
            DisplayName = ApprovalWorkflowDomainValues.DisplayDeptHead,
            IsEnabled = false
        };

        public ApprovalSignerSlot ArchiveRoomHead { get; init; } = new()
        {
            NodeKey = ApprovalWorkflowDomainValues.NodeArchiveRoomHead,
            DisplayName = ApprovalWorkflowDomainValues.DisplayArchiveRoomHead,
            IsEnabled = false
        };

        public ApprovalSignerSlot ProductionHead { get; init; } = new()
        {
            NodeKey = ApprovalWorkflowDomainValues.NodeProductionHead,
            DisplayName = ApprovalWorkflowDomainValues.DisplayProductionHead,
            IsEnabled = false
        };

        public ApprovalSignerSlot ArchiveDeputyPresident { get; init; } = new()
        {
            NodeKey = ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident,
            DisplayName = ApprovalWorkflowDomainValues.DisplayArchiveDeputyPresident,
            IsEnabled = false
        };

        public ApprovalSignerSlot ProductionVicePresident { get; init; } = new()
        {
            NodeKey = ApprovalWorkflowDomainValues.NodeProductionVicePresident,
            DisplayName = ApprovalWorkflowDomainValues.DisplayProductionVicePresident,
            IsEnabled = false
        };

        public bool HasDepartmentLevel => DeptHead.IsEnabled;

        public bool HasFunctionalLevel => ArchiveRoomHead.IsEnabled || ProductionHead.IsEnabled;

        public bool HasInstituteLevel => ArchiveDeputyPresident.IsEnabled || ProductionVicePresident.IsEnabled;

        public IEnumerable<ApprovalSignerSlot> EnabledSigners()
        {
            if (DeptHead.IsEnabled) yield return DeptHead;
            if (ArchiveRoomHead.IsEnabled) yield return ArchiveRoomHead;
            if (ProductionHead.IsEnabled) yield return ProductionHead;
            if (ArchiveDeputyPresident.IsEnabled) yield return ArchiveDeputyPresident;
            if (ProductionVicePresident.IsEnabled) yield return ProductionVicePresident;
        }
    }

    /// <summary>规则解析入参。</summary>
    public sealed class ApprovalChainResolveRequest
    {
        public required string BusinessType { get; init; }

        /// <summary>单据字段快照（键为条件字段 FieldKey）。</summary>
        public IReadOnlyDictionary<string, string> FieldValues { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>申请人部门（用于部门负责人回退解析）。</summary>
        public string? ApplicantDept { get; init; }
    }

    /// <summary>打印：一级一行。</summary>
    public sealed class ApprovalPrintLevelRow
    {
        public required string LevelTitle { get; init; }

        public required IReadOnlyList<(string Label, string Name)> Signers { get; init; }
    }

    /// <summary>按「一级一行、未启用整栏隐藏」生成打印行。</summary>
    public static class ApprovalChainPrintLayoutSupport
    {
        public static IReadOnlyList<ApprovalPrintLevelRow> BuildRows(ApprovalChainResolution chain)
        {
            ArgumentNullException.ThrowIfNull(chain);

            var rows = new List<ApprovalPrintLevelRow>(3);
            if (chain.HasDepartmentLevel)
            {
                rows.Add(new ApprovalPrintLevelRow
                {
                    LevelTitle = "部门审核",
                    Signers = [(chain.DeptHead.DisplayName, chain.DeptHead.DefaultRealName)]
                });
            }

            if (chain.HasFunctionalLevel)
            {
                var signers = new List<(string, string)>(2);
                if (chain.ArchiveRoomHead.IsEnabled)
                {
                    signers.Add((chain.ArchiveRoomHead.DisplayName, chain.ArchiveRoomHead.DefaultRealName));
                }

                if (chain.ProductionHead.IsEnabled)
                {
                    signers.Add((chain.ProductionHead.DisplayName, chain.ProductionHead.DefaultRealName));
                }

                rows.Add(new ApprovalPrintLevelRow
                {
                    LevelTitle = "职能部门审核",
                    Signers = signers
                });
            }

            if (chain.HasInstituteLevel)
            {
                var signers = new List<(string, string)>(2);
                if (chain.ArchiveDeputyPresident.IsEnabled)
                {
                    signers.Add((chain.ArchiveDeputyPresident.DisplayName, chain.ArchiveDeputyPresident.DefaultRealName));
                }

                if (chain.ProductionVicePresident.IsEnabled)
                {
                    signers.Add((chain.ProductionVicePresident.DisplayName, chain.ProductionVicePresident.DefaultRealName));
                }

                rows.Add(new ApprovalPrintLevelRow
                {
                    LevelTitle = "院级审批",
                    Signers = signers
                });
            }

            return rows;
        }
    }
}
