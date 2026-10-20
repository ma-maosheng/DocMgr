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
                ApprovalWorkflowDomainValues.NodeArchiveRoomHead => FindByExactRole(
                    users,
                    UserRoleDomainValues.ArchiveRoomHead,
                    UserRoleDomainValues.ArchiveRoomDepartment),
                ApprovalWorkflowDomainValues.NodeProductionHead => FindByExactRole(
                    users,
                    UserRoleDomainValues.ProductionHead,
                    UserRoleDomainValues.ProductionManagementDepartment),
                ApprovalWorkflowDomainValues.NodeArchiveDeputyPresident => FindByExactRole(
                    users,
                    UserRoleDomainValues.ArchiveDeputyPresident,
                    UserRoleDomainValues.LeadershipDepartment),
                ApprovalWorkflowDomainValues.NodeProductionVicePresident => FindByExactRole(
                    users,
                    UserRoleDomainValues.ProductionVicePresident,
                    UserRoleDomainValues.LeadershipDepartment),
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
            if (!string.IsNullOrWhiteSpace(dept))
            {
                string head = users
                    .FirstOrDefault(user =>
                        string.Equals(user.Department?.Trim(), dept, StringComparison.Ordinal)
                        && string.Equals(user.Role?.Trim(), UserRoleDomainValues.DepartmentHead, StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(user.RealName))
                    ?.RealName
                    ?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(head))
                {
                    return head;
                }
            }

            return FindByExactRole(users, UserRoleDomainValues.DepartmentHead, preferredDepartment: null);
        }

        private static string FindByExactRole(
            IReadOnlyList<User> users,
            string roleName,
            string? preferredDepartment)
        {
            if (!string.IsNullOrWhiteSpace(preferredDepartment))
            {
                string preferred = users
                    .FirstOrDefault(user =>
                        string.Equals(user.Role?.Trim(), roleName, StringComparison.Ordinal)
                        && string.Equals(user.Department?.Trim(), preferredDepartment, StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(user.RealName))
                    ?.RealName
                    ?.Trim() ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(preferred))
                {
                    return preferred;
                }
            }

            return users
                .FirstOrDefault(user =>
                    string.Equals(user.Role?.Trim(), roleName, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(user.RealName))
                ?.RealName
                ?.Trim() ?? string.Empty;
        }
    }
}
