namespace DocMgr.Models.SystemSettings
{
    /// <summary>规则条件匹配与签批人姓名回退解析。</summary>
    public static class ApprovalWorkflowMatchingSupport
    {
        public static bool IsRuleMatch(ApprovalWorkflowRule rule, IReadOnlyDictionary<string, string> fieldValues)
        {
            ArgumentNullException.ThrowIfNull(rule);
            ArgumentNullException.ThrowIfNull(fieldValues);

            bool has1 = HasCondition(rule.Condition1FieldKey, rule.Condition1Value);
            bool has2 = HasCondition(rule.Condition2FieldKey, rule.Condition2Value);

            if (!has1 && !has2)
            {
                return true;
            }

            bool match1 = !has1 || FieldEquals(fieldValues, rule.Condition1FieldKey, rule.Condition1Value);
            bool match2 = !has2 || FieldEquals(fieldValues, rule.Condition2FieldKey, rule.Condition2Value);

            if (has1 && has2)
            {
                return string.Equals(rule.ConditionLogic?.Trim(), ApprovalWorkflowDomainValues.ConditionLogicOr, StringComparison.Ordinal)
                    ? match1 || match2
                    : match1 && match2;
            }

            return has1 ? match1 : match2;
        }

        public static string ResolveRealName(
            IReadOnlyList<User> users,
            int? configuredUserId,
            string? applicantDept,
            string nodeKey)
        {
            ArgumentNullException.ThrowIfNull(users);

            if (configuredUserId is > 0)
            {
                string? configured = users
                    .FirstOrDefault(user => user.Id == configuredUserId.Value)
                    ?.RealName
                    ?.Trim();
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return configured;
                }
            }

            return nodeKey switch
            {
                ApprovalWorkflowDomainValues.NodeDeptHead => FindDeptHead(users, applicantDept),
                ApprovalWorkflowDomainValues.NodeArchiveRoomHead => FindArchiveRoomHead(users),
                ApprovalWorkflowDomainValues.NodeProductionHead => FindByRoleOrDept(users, "生产管理科"),
                ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident => FindByRoleOrDept(users, "分管资料副院长"),
                ApprovalWorkflowDomainValues.NodeProductionVicePresident => FindByRoleOrDept(users, "分管生产副院长"),
                _ => string.Empty
            };
        }

        private static bool HasCondition(string? fieldKey, string? value) =>
            !string.IsNullOrWhiteSpace(fieldKey) && !string.IsNullOrWhiteSpace(value);

        private static bool FieldEquals(IReadOnlyDictionary<string, string> fieldValues, string fieldKey, string expected)
        {
            if (!fieldValues.TryGetValue(fieldKey.Trim(), out string? actual) || string.IsNullOrWhiteSpace(actual))
            {
                return false;
            }

            return string.Equals(actual.Trim(), expected.Trim(), StringComparison.Ordinal);
        }

        private static string FindDeptHead(IReadOnlyList<User> users, string? applicantDept)
        {
            string dept = applicantDept?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(dept))
            {
                return FindByRoleOrDept(users, "部门负责人");
            }

            string head = users
                .FirstOrDefault(user =>
                    string.Equals(user.Department?.Trim(), dept, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(user.RealName)
                    && (user.Role?.Contains("部门负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                ?.RealName
                ?.Trim() ?? string.Empty;

            return string.IsNullOrWhiteSpace(head)
                ? FindByRoleOrDept(users, "部门负责人")
                : head;
        }

        private static string FindArchiveRoomHead(IReadOnlyList<User> users)
        {
            string head = users
                .FirstOrDefault(user =>
                    string.Equals(user.Department?.Trim(), "资料室", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(user.RealName)
                    && (user.Role?.Contains("负责人", StringComparison.OrdinalIgnoreCase) ?? false))
                ?.RealName
                ?.Trim() ?? string.Empty;

            return string.IsNullOrWhiteSpace(head)
                ? FindByRoleOrDept(users, "资料室")
                : head;
        }

        private static string FindByRoleOrDept(IReadOnlyList<User> users, string keyword)
        {
            return users
                .FirstOrDefault(user =>
                    (user.Role?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (user.Department?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                ?.RealName
                ?.Trim() ?? string.Empty;
        }
    }
}
