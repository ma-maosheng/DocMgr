using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveMaterialTransactionConfiguration : IEntityTypeConfiguration<YearlyArchiveMaterialTransaction>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveMaterialTransaction> builder)
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.TransactionType).HasMaxLength(32);
            builder.Property(item => item.BusinessNo).HasMaxLength(64);
            builder.Property(item => item.SourceKind).HasMaxLength(32);
            builder.Property(item => item.DedupKey).HasMaxLength(128);
            builder.Property(item => item.BeforeLifecycleStatus).HasMaxLength(32);
            builder.Property(item => item.AfterLifecycleStatus).HasMaxLength(32);
            builder.Property(item => item.BeforeContainerCode).HasMaxLength(64);
            builder.Property(item => item.AfterContainerCode).HasMaxLength(64);
            builder.Property(item => item.BeforeStorageLocation).HasMaxLength(128);
            builder.Property(item => item.AfterStorageLocation).HasMaxLength(128);
            builder.Property(item => item.Summary).HasMaxLength(512);
            builder.Property(item => item.Remark).HasMaxLength(512);
            builder.Property(item => item.OperatorName).HasMaxLength(64);
            builder.HasIndex(item => item.DedupKey).IsUnique();
            builder.HasIndex(item => new { item.FilingFactId, item.OperatedAt });
        }
    }
}
