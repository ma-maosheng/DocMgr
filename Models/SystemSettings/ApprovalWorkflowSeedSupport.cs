using DocMgr.Models.NetworkTransfer;

namespace DocMgr.Models.SystemSettings
{
    /// <summary>按现行硬编码行为生成默认审核审批规则（供首次种子与恢复默认）。</summary>
    public static class ApprovalWorkflowSeedSupport
    {
        public static IReadOnlyList<ApprovalWorkflowRule> CreateDefaultRules(DateTime now)
        {
            var rules = new List<ApprovalWorkflowRule>();

            // 建档：部门 + 生产科 + 资料室 + 分管资料副院长
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.YearlyArchiveRegister,
                "默认（年度资料档案化管理·资料建档·建档申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: true,
                enableProductionVp: false,
                now));

            // 出库：部门 + 资料室 + 生产科 + 分管生产副院长
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.YearlyArchiveOutbound,
                "默认（年度资料档案化管理·资料流转·借出申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: false,
                enableProductionVp: true,
                now));

            // 归还-灭失
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.YearlyArchiveReturn,
                "灭失归还",
                priority: 10,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: false,
                enableProductionVp: true,
                now,
                field1: ApprovalWorkflowDomainValues.FieldHasLoss,
                value1: ApprovalWorkflowDomainValues.Yes));

            // 归还-完好
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.YearlyArchiveReturn,
                "完好归还",
                priority: 20,
                enableDept: true,
                enableArchiveRoom: false,
                enableProduction: false,
                enableArchiveVp: false,
                enableProductionVp: false,
                now,
                field1: ApprovalWorkflowDomainValues.FieldHasLoss,
                value1: ApprovalWorkflowDomainValues.No));

            // 归还默认兜底（按完好）
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.YearlyArchiveReturn,
                "默认（年度资料档案化管理·资料流转·归还申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: false,
                enableProduction: false,
                enableArchiveVp: false,
                enableProductionVp: false,
                now));

            // 入网：同建档
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.NetworkInbound,
                "默认（年度资料出入网管理·入网申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: true,
                enableProductionVp: false,
                now));

            // 出网：出网（院内/院外）→ 分管生产；其余 → 分管资料
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.NetworkOutbound,
                "出网（院内）",
                priority: 10,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: false,
                enableProductionVp: true,
                now,
                field1: ApprovalWorkflowDomainValues.FieldDestinationKind,
                value1: NetworkTransferDomainValues.DestinationKindOutboundInternal));

            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.NetworkOutbound,
                "出网（院外）",
                priority: 11,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: false,
                enableProductionVp: true,
                now,
                field1: ApprovalWorkflowDomainValues.FieldDestinationKind,
                value1: NetworkTransferDomainValues.DestinationKindOutboundExternal));

            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.NetworkOutbound,
                "默认（年度资料出入网管理·出网申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: true,
                enableProductionVp: false,
                now));

            // 年度离库：资料室 + 生产科 + 两位副院长
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal,
                "默认（年度资料档案化管理·离库处置·模拟/电子资料离库处置）",
                priority: 100,
                enableDept: false,
                enableArchiveRoom: true,
                enableProduction: true,
                enableArchiveVp: true,
                enableProductionVp: true,
                now));

            // 历史存档 / 在网处置：资料室 + 分管资料副院长
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal,
                "默认（历史存档资料管理·资料离库处置）",
                priority: 100,
                enableDept: false,
                enableArchiveRoom: true,
                enableProduction: false,
                enableArchiveVp: true,
                enableProductionVp: false,
                now));

            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal,
                "默认（年度资料出入网管理·在网数据处置）",
                priority: 100,
                enableDept: false,
                enableArchiveRoom: true,
                enableProduction: false,
                enableArchiveVp: true,
                enableProductionVp: false,
                now));

            // 硬盘出库：部门负责人 + 资料室负责人
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.HardDiskOutbound,
                "默认（介质管理·硬盘·出库申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: false,
                enableArchiveVp: false,
                enableProductionVp: false,
                now));

            // 硬盘归还：部门负责人 + 资料室负责人（与出库默认链一致）
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.HardDiskReturn,
                "默认（介质管理·硬盘·归还申请）",
                priority: 100,
                enableDept: true,
                enableArchiveRoom: true,
                enableProduction: false,
                enableArchiveVp: false,
                enableProductionVp: false,
                now));

            // 硬盘离库：资料室负责人（打印侧可再扩展）
            rules.Add(Create(
                ApprovalWorkflowBusinessTypes.HardDiskDisposal,
                "默认（介质管理·硬盘·离库处置）",
                priority: 100,
                enableDept: false,
                enableArchiveRoom: true,
                enableProduction: false,
                enableArchiveVp: true,
                enableProductionVp: false,
                now));

            return rules;
        }

        private static ApprovalWorkflowRule Create(
            string businessType,
            string ruleName,
            int priority,
            bool enableDept,
            bool enableArchiveRoom,
            bool enableProduction,
            bool enableArchiveVp,
            bool enableProductionVp,
            DateTime now,
            string field1 = "",
            string value1 = "",
            string field2 = "",
            string value2 = "",
            string logic = ApprovalWorkflowDomainValues.ConditionLogicAnd)
        {
            return new ApprovalWorkflowRule
            {
                BusinessType = businessType,
                RuleName = ruleName,
                Priority = priority,
                ConditionLogic = logic,
                Condition1FieldKey = field1,
                Condition1Value = value1,
                Condition2FieldKey = field2,
                Condition2Value = value2,
                EnableDeptHead = enableDept,
                EnableArchiveRoomHead = enableArchiveRoom,
                EnableProductionHead = enableProduction,
                EnableArchiveDeputyPresident = enableArchiveVp,
                EnableProductionVicePresident = enableProductionVp,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
