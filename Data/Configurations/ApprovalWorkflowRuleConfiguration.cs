using DocMgr.Models.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class ApprovalWorkflowRuleConfiguration : IEntityTypeConfiguration<ApprovalWorkflowRule>
    {
        public void Configure(EntityTypeBuilder<ApprovalWorkflowRule> builder)
        {
            builder.ToTable("ApprovalWorkflowRules");
            builder.HasKey(item => item.Id);

            builder.Property(item => item.BusinessType).HasMaxLength(64).IsRequired();
            builder.Property(item => item.RuleName).HasMaxLength(128).IsRequired();
            builder.Property(item => item.ConditionLogic).HasMaxLength(16).IsRequired();
            builder.Property(item => item.Condition1FieldKey).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Condition1Value).HasMaxLength(128).IsRequired();
            builder.Property(item => item.Condition2FieldKey).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Condition2Value).HasMaxLength(128).IsRequired();

            builder.HasIndex(item => new { item.BusinessType, item.Priority, item.IsEnabled });
        }
    }
}
