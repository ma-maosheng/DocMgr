namespace DocMgr.Models.SystemSettings
{
    /// <summary>
    /// 审核审批规则：按业务类型 + 最多两个条件字段匹配，决定三级签批链与默认人员。
    /// </summary>
    public class ApprovalWorkflowRule
    {
        public int Id { get; set; }

        /// <summary>业务类型编码，见 <see cref="ApprovalWorkflowBusinessTypes"/>。</summary>
        public string BusinessType { get; set; } = string.Empty;

        /// <summary>规则名称（管理端展示）。</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>优先级，数值越小越优先；冲突时取第一条命中。</summary>
        public int Priority { get; set; } = 100;

        /// <summary>两条件逻辑：And / Or；仅一个条件时忽略。</summary>
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

        /// <summary>默认部门负责人用户 Id；空则按申请人部门解析。</summary>
        public int? DefaultDeptHeadUserId { get; set; }

        public int? DefaultArchiveRoomHeadUserId { get; set; }

        public int? DefaultProductionHeadUserId { get; set; }

        public int? DefaultArchiveDeputyPresidentUserId { get; set; }

        public int? DefaultProductionVicePresidentUserId { get; set; }

        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}
