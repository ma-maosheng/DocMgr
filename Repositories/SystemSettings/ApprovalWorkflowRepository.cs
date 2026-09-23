using DocMgr.Data;
using DocMgr.Models.SystemSettings;
using DocMgr.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DocMgr.Repositories.SystemSettings
{
    public class ApprovalWorkflowRepository : IApprovalWorkflowRepository
    {
        private readonly AppDbContext _dbContext;

        public ApprovalWorkflowRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<ApprovalWorkflowRule>> GetAllAsync()
        {
            return await _dbContext.ApprovalWorkflowRules
                .AsNoTracking()
                .OrderBy(item => item.BusinessType)
                .ThenBy(item => item.Priority)
                .ThenBy(item => item.Id)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<ApprovalWorkflowRule>> GetByBusinessTypeAsync(string businessType)
        {
            string key = businessType?.Trim() ?? string.Empty;
            return await _dbContext.ApprovalWorkflowRules
                .AsNoTracking()
                .Where(item => item.BusinessType == key)
                .OrderBy(item => item.Priority)
                .ThenBy(item => item.Id)
                .ToListAsync();
        }

        public Task<ApprovalWorkflowRule?> GetByIdAsync(int id) =>
            _dbContext.ApprovalWorkflowRules.FirstOrDefaultAsync(item => item.Id == id);

        public Task<int> CountAsync() => _dbContext.ApprovalWorkflowRules.CountAsync();

        public async Task AddRangeAsync(IEnumerable<ApprovalWorkflowRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            await _dbContext.ApprovalWorkflowRules.AddRangeAsync(rules);
            await _dbContext.SaveChangesAsync();
        }

        public async Task AddAsync(ApprovalWorkflowRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            _dbContext.ApprovalWorkflowRules.Add(rule);
            await _dbContext.SaveChangesAsync();
        }

        public async Task UpdateAsync(ApprovalWorkflowRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            _dbContext.ApprovalWorkflowRules.Update(rule);
            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(ApprovalWorkflowRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);
            _dbContext.ApprovalWorkflowRules.Remove(rule);
            await _dbContext.SaveChangesAsync();
        }

        public async Task ReplaceAllAsync(IEnumerable<ApprovalWorkflowRule> rules)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var existing = await _dbContext.ApprovalWorkflowRules.ToListAsync();
            if (existing.Count > 0)
            {
                _dbContext.ApprovalWorkflowRules.RemoveRange(existing);
            }

            await _dbContext.ApprovalWorkflowRules.AddRangeAsync(rules);
            await _dbContext.SaveChangesAsync();
            // 整表替换后清空跟踪，避免后续 AsNoTracking 查询与已删除实体纠缠。
            _dbContext.ChangeTracker.Clear();
        }
    }
}
