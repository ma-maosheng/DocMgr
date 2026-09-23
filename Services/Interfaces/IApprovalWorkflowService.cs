using DocMgr.Models.Shared;
using DocMgr.Models.SystemSettings;

namespace DocMgr.Services.Interfaces
{
    /// <summary>审核审批规则配置与签批链解析。</summary>
    public interface IApprovalWorkflowService
    {
        IReadOnlyList<ApprovalBusinessTypeDefinition> GetBusinessTypeCatalog();

        Task EnsureSeededAsync();

        Task<IReadOnlyList<ApprovalWorkflowRule>> GetRulesAsync(string? businessType = null);

        Task<ApprovalWorkflowRule?> GetRuleAsync(int id);

        Task SaveRuleAsync(ApprovalWorkflowRule rule, User operatorUser);

        Task DeleteRuleAsync(int id, User operatorUser);

        Task ResetToDefaultsAsync(User operatorUser);

        /// <summary>导出全部审核审批规则为 JSON 文本（不含数据库主键）。</summary>
        Task<string> ExportAllRulesJsonAsync(User operatorUser);

        /// <summary>用 JSON 包覆盖导入全部审核审批规则（先清空再写入）。</summary>
        Task ImportAllRulesFromJsonAsync(string json, User operatorUser);

        /// <summary>
        /// 按优先级解析签批链。已办结/作废单据应传 <paramref name="workflowStatus"/> 为终态，
        /// 此时仍可解析用于对照，但调用方应优先使用单据姓名快照。
        /// </summary>
        Task<ApprovalChainResolution> ResolveAsync(ApprovalChainResolveRequest request, IReadOnlyList<User> users);

        /// <summary>工作流状态是否为终态（已办结/作废），配置变更不再驱动预填与校验。</summary>
        bool IsTerminalWorkflowStatus(int status);
    }
}
