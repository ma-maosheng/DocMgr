using DocMgr.Models.SystemSettings;

namespace DocMgr.Models.YearlyArchive
{
    /// <summary>
    /// 资料离库处置签批单默认审核/审批人。
    /// </summary>
    public sealed class ArchiveDisposalDefaultApprovers
    {
        /// <summary>部门审核。</summary>
        public string DeptHead { get; init; } = string.Empty;

        /// <summary>资料室负责人（审核）。</summary>
        public string ArchiveRoomHead { get; init; } = string.Empty;

        /// <summary>生产科负责人（审核）。</summary>
        public string ProductionHead { get; init; } = string.Empty;

        /// <summary>分管资料室副院长（审批）。</summary>
        public string ArchiveDeputyPresident { get; init; } = string.Empty;

        /// <summary>分管生产副院长（审批）。</summary>
        public string ProductionVicePresident { get; init; } = string.Empty;

        public bool EnableDeptHead { get; init; }

        public bool EnableArchiveRoomHead { get; init; } = true;

        public bool EnableProductionHead { get; init; } = true;

        public bool EnableArchiveDeputyPresident { get; init; } = true;

        public bool EnableProductionVicePresident { get; init; } = true;
    }

    /// <summary>
    /// 解析资料离库处置默认审核/审批人（配置链或关键字回退）。
    /// </summary>
    public static class ArchiveDisposalDefaultApproverSupport
    {
        /// <summary>由签批链解析结果转换。</summary>
        public static ArchiveDisposalDefaultApprovers FromChain(ApprovalChainResolution chain)
        {
            ArgumentNullException.ThrowIfNull(chain);
            return new ArchiveDisposalDefaultApprovers
            {
                DeptHead = chain.DeptHead.IsEnabled ? chain.DeptHead.DefaultRealName : string.Empty,
                ArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled ? chain.ArchiveRoomHead.DefaultRealName : string.Empty,
                ProductionHead = chain.ProductionHead.IsEnabled ? chain.ProductionHead.DefaultRealName : string.Empty,
                ArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled
                    ? chain.ArchiveDeputyPresident.DefaultRealName
                    : string.Empty,
                ProductionVicePresident = chain.ProductionVicePresident.IsEnabled
                    ? chain.ProductionVicePresident.DefaultRealName
                    : string.Empty,
                EnableDeptHead = chain.DeptHead.IsEnabled,
                EnableArchiveRoomHead = chain.ArchiveRoomHead.IsEnabled,
                EnableProductionHead = chain.ProductionHead.IsEnabled,
                EnableArchiveDeputyPresident = chain.ArchiveDeputyPresident.IsEnabled,
                EnableProductionVicePresident = chain.ProductionVicePresident.IsEnabled
            };
        }

        /// <summary>按用户表角色/部门关键字解析默认签字人（无配置时的回退）。</summary>
        public static ArchiveDisposalDefaultApprovers Resolve(IReadOnlyList<User> users)
        {
            ArgumentNullException.ThrowIfNull(users);
            var list = users as List<User> ?? users.ToList();
            return new ArchiveDisposalDefaultApprovers
            {
                DeptHead = FindByRoleOrDept(list, "部门负责人"),
                ArchiveRoomHead = FindArchiveRoomHead(list),
                ProductionHead = FindByRoleOrDept(list, "生产管理科"),
                ArchiveDeputyPresident = FindByRoleOrDept(list, "分管资料副院长"),
                ProductionVicePresident = FindByRoleOrDept(list, "分管生产副院长"),
                EnableDeptHead = false,
                EnableArchiveRoomHead = true,
                EnableProductionHead = true,
                EnableArchiveDeputyPresident = true,
                EnableProductionVicePresident = true
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
