using DocMgr.Models.SystemSettings;

namespace DocMgr.Repositories.Interfaces
{
    /// <summary>审核审批规则仓储。</summary>
    public interface IApprovalWorkflowRepository
    {
        Task<IReadOnlyList<ApprovalWorkflowRule>> GetAllAsync();

        Task<IReadOnlyList<ApprovalWorkflowRule>> GetByBusinessTypeAsync(string businessType);

        Task<ApprovalWorkflowRule?> GetByIdAsync(int id);

        Task<int> CountAsync();

        Task AddRangeAsync(IEnumerable<ApprovalWorkflowRule> rules);

        Task AddAsync(ApprovalWorkflowRule rule);

        Task UpdateAsync(ApprovalWorkflowRule rule);

        Task DeleteAsync(ApprovalWorkflowRule rule);

        Task ReplaceAllAsync(IEnumerable<ApprovalWorkflowRule> rules);
    }
}
