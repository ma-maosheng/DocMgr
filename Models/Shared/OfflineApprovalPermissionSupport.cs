using DocMgr.Models.SystemSettings;

namespace DocMgr.Models.Shared
{
    /// <summary>
    /// 线下签批发起人/办理人权限策略（按业务类型）。
    /// 角色判定与 <see cref="DocMgr.Services.YearlyArchive.ArchiveRegisterBusinessRules"/> 同源：仅认角色，系统管理员不替代。
    /// </summary>
    public static class OfflineApprovalPermissionSupport
    {
        /// <summary>资料管理员：审批工作台与 B 流发起。</summary>
        public static bool IsArchiveAdmin(User? user)
        {
            if (user == null)
            {
                return false;
            }

            string role = user.Role?.Trim() ?? string.Empty;
            return string.Equals(role, UserRoleDomainValues.ArchiveAdmin, StringComparison.Ordinal);
        }

        /// <summary>部门资料员：A 流发起申请。</summary>
        public static bool IsApplicantClerk(User? user)
        {
            if (user == null)
            {
                return false;
            }

            string role = user.Role?.Trim() ?? string.Empty;
            return string.Equals(role, UserRoleDomainValues.DepartmentArchiveClerk, StringComparison.Ordinal);
        }

        /// <summary>是否允许新建草稿：A 流=部门资料员；B 流（离库处置）=资料管理员。</summary>
        public static bool CanCreateDraft(User? user, string? businessType)
        {
            if (IsDisposalBusiness(businessType))
            {
                return IsArchiveAdmin(user);
            }

            return IsApplicantClerk(user);
        }

        /// <summary>是否可进入审批办理工作台（资料管理员）。</summary>
        public static bool CanOperateApprovalWorkbench(User? user, string? businessType = null)
        {
            _ = businessType;
            return IsArchiveAdmin(user);
        }

        /// <summary>申请侧列表操作（提交/撤回等）是否允许：部门资料员。</summary>
        public static bool CanApplicantOperate(User? user) => IsApplicantClerk(user);

        /// <summary>办结后增补「其他附件」：资料管理员。</summary>
        public static bool CanSupplementOtherAfterComplete(User? user) => IsArchiveAdmin(user);

        /// <summary>是否为离库处置类业务（B 流）。</summary>
        public static bool IsDisposalBusiness(string? businessType)
        {
            string key = businessType?.Trim() ?? string.Empty;
            return string.Equals(key, ApprovalWorkflowBusinessTypes.YearlyArchiveDisposal, StringComparison.Ordinal)
                   || string.Equals(key, ApprovalWorkflowBusinessTypes.HistoryArchiveDisposal, StringComparison.Ordinal)
                   || string.Equals(key, ApprovalWorkflowBusinessTypes.NetworkOnNetDisposal, StringComparison.Ordinal)
                   || string.Equals(key, ApprovalWorkflowBusinessTypes.HardDiskDisposal, StringComparison.Ordinal)
                   || string.Equals(key, ApprovalWorkflowBusinessTypes.HardDiskInventoryRegister, StringComparison.Ordinal)
                   || string.Equals(key, ApprovalWorkflowBusinessTypes.YearlyArchiveInventoryRegister, StringComparison.Ordinal)
                   || string.Equals(key, "ArchiveDisposal", StringComparison.Ordinal)
                   || string.Equals(key, "HardDiskDisposal", StringComparison.Ordinal)
                   || string.Equals(key, "HardDiskInventoryRegister", StringComparison.Ordinal)
                   || string.Equals(key, "ArchiveInventoryRegister", StringComparison.Ordinal);
        }
    }
}
