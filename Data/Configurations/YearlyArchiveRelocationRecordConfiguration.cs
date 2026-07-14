using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveRelocationRecordConfiguration : IEntityTypeConfiguration<YearlyArchiveRelocationRecord>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRelocationRecord> builder)
        {
            builder.HasKey(record => record.Id);
            builder.HasIndex(record => record.RelocationNo).IsUnique();
            builder.Property(record => record.RelocationNo).HasMaxLength(64);
            builder.Property(record => record.MediaKind).HasMaxLength(16);
            builder.Property(record => record.RelocationMode).HasMaxLength(32);
            builder.Property(record => record.SourceContainerCode).HasMaxLength(64);
            builder.Property(record => record.SourceStorageLocation).HasMaxLength(128);
            builder.Property(record => record.TargetContainerCode).HasMaxLength(64);
            builder.Property(record => record.TargetStorageLocation).HasMaxLength(128);
            builder.Property(record => record.SourceMediumDisposition).HasMaxLength(64);
            builder.Property(record => record.OperatedBy).HasMaxLength(64);
            builder.Property(record => record.Remarks).HasMaxLength(512);
            builder.HasMany(record => record.Items)
                .WithOne(item => item.RelocationRecord)
                .HasForeignKey(item => item.RelocationRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
