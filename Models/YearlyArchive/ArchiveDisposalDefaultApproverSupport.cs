using DocMgr.Models.SystemSettings;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料离库处置签批单默认审核/审批人（按用户角色/部门解析，供界面展示与办结后重打预填）。
    /// </summary>
    public sealed class ArchiveDisposalDefaultApprovers
    {
        /// <summary>资料室负责人（审核）。</summary>
        public string ArchiveRoomHead { get; init; } = string.Empty;

        /// <summary>生产科负责人（审核）。</summary>
        public string ProductionHead { get; init; } = string.Empty;

        /// <summary>分管资料室副院长（审批）。</summary>
        public string ArchiveDeputyPresident { get; init; } = string.Empty;

        /// <summary>分管生产副院长（审批）。</summary>
        public string ProductionVicePresident { get; init; } = string.Empty;
    }

    /// <summary>
    /// 解析资料离库处置默认审核/审批人（口径对齐年度资料登记/出库默认审批解析）。
    /// </summary>
    public static class ArchiveDisposalDefaultApproverSupport
    {
        /// <summary>按用户表角色/部门解析四位默认签字人。</summary>
        public static ArchiveDisposalDefaultApprovers Resolve(IReadOnlyList<User> users)
        {
            ArgumentNullException.ThrowIfNull(users);
            var list = users as List<User> ?? users.ToList();
            return new ArchiveDisposalDefaultApprovers
            {
                ArchiveRoomHead = FindArchiveRoomHead(list),
                ProductionHead = FindByRoleOrDept(list, "生产管理科"),
                ArchiveDeputyPresident = FindByRoleOrDept(list, "分管资料副院长"),
                ProductionVicePresident = FindByRoleOrDept(list, "分管生产副院长")
            };
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
