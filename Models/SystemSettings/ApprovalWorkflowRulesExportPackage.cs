namespace DocMgr.Models.SystemSettings
{
    /// <summary>审核审批规则 JSON 整表导出包。</summary>
    public sealed class ApprovalWorkflowRulesExportPackage
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime ExportedAt { get; set; }

        public List<ApprovalWorkflowRuleExportItem> Rules { get; set; } = [];
    }

    /// <summary>导出项：不含数据库主键与时间戳，导入时整表覆盖重建。</summary>
    public sealed class ApprovalWorkflowRuleExportItem
    {
        public string BusinessType { get; set; } = string.Empty;

        public string RuleName { get; set; } = string.Empty;

        public int Priority { get; set; } = 100;

        public string ConditionLogic { get; set; } = ApprovalWorkflowDomainValues.ConditionLogicAnd;

        public string Condition1FieldKey { get; set; } = string.Empty;

        public string Condition1Value { get; set; } = string.Empty;

        public string Condition2FieldKey { get; set; } = string.Empty;

        public string Condition2Value { get; set; } = string.Empty;

        public bool EnableDeptHead { get; set; } = true;

        public bool EnableArchiveRoomHead { get; set; }

        public bool EnableProductionHead { get; set; }

        public bool EnableArchiveDeputyPresident { get; set; }

        public bool EnableProductionVicePresident { get; set; }

        public int? DefaultDeptHeadUserId { get; set; }

        public int? DefaultArchiveRoomHeadUserId { get; set; }

        public int? DefaultProductionHeadUserId { get; set; }

        public int? DefaultArchiveDeputyPresidentUserId { get; set; }

        public int? DefaultProductionVicePresidentUserId { get; set; }

        public bool IsEnabled { get; set; } = true;

        public static ApprovalWorkflowRuleExportItem FromEntity(ApprovalWorkflowRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            return new ApprovalWorkflowRuleExportItem
            {
                BusinessType = rule.BusinessType,
                RuleName = rule.RuleName,
                Priority = rule.Priority,
                ConditionLogic = rule.ConditionLogic,
                Condition1FieldKey = rule.Condition1FieldKey,
                Condition1Value = rule.Condition1Value,
                Condition2FieldKey = rule.Condition2FieldKey,
                Condition2Value = rule.Condition2Value,
                EnableDeptHead = rule.EnableDeptHead,
                EnableArchiveRoomHead = rule.EnableArchiveRoomHead,
                EnableProductionHead = rule.EnableProductionHead,
                EnableArchiveDeputyPresident = rule.EnableArchiveDeputyPresident,
                EnableProductionVicePresident = rule.EnableProductionVicePresident,
                DefaultDeptHeadUserId = rule.DefaultDeptHeadUserId,
                DefaultArchiveRoomHeadUserId = rule.DefaultArchiveRoomHeadUserId,
                DefaultProductionHeadUserId = rule.DefaultProductionHeadUserId,
                DefaultArchiveDeputyPresidentUserId = rule.DefaultArchiveDeputyPresidentUserId,
                DefaultProductionVicePresidentUserId = rule.DefaultProductionVicePresidentUserId,
                IsEnabled = rule.IsEnabled
            };
        }

        public ApprovalWorkflowRule ToEntity(DateTime now)
        {
            return new ApprovalWorkflowRule
            {
                Id = 0,
                BusinessType = BusinessType,
                RuleName = RuleName,
                Priority = Priority,
                ConditionLogic = ConditionLogic,
                Condition1FieldKey = Condition1FieldKey,
                Condition1Value = Condition1Value,
                Condition2FieldKey = Condition2FieldKey,
                Condition2Value = Condition2Value,
                EnableDeptHead = EnableDeptHead,
                EnableArchiveRoomHead = EnableArchiveRoomHead,
                EnableProductionHead = EnableProductionHead,
                EnableArchiveDeputyPresident = EnableArchiveDeputyPresident,
                EnableProductionVicePresident = EnableProductionVicePresident,
                DefaultDeptHeadUserId = DefaultDeptHeadUserId,
                DefaultArchiveRoomHeadUserId = DefaultArchiveRoomHeadUserId,
                DefaultProductionHeadUserId = DefaultProductionHeadUserId,
                DefaultArchiveDeputyPresidentUserId = DefaultArchiveDeputyPresidentUserId,
                DefaultProductionVicePresidentUserId = DefaultProductionVicePresidentUserId,
                IsEnabled = IsEnabled,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
