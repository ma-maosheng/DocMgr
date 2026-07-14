using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveOutboundRecordConfiguration : IEntityTypeConfiguration<YearlyArchiveOutboundRecord>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveOutboundRecord> builder)
        {
            builder.HasKey(record => record.Id);
            builder.HasIndex(record => record.OutboundNo).IsUnique();
            builder.Property(record => record.OutboundNo).HasMaxLength(64);
            builder.Property(record => record.ProjectName).HasMaxLength(256);
            builder.Property(record => record.ApplicantName).HasMaxLength(64);
            builder.Property(record => record.ApplicantDept).HasMaxLength(128);
            builder.Property(record => record.Reason).HasMaxLength(512);
            builder.Property(record => record.DestinationKind).HasMaxLength(32);
            builder.Property(record => record.ExternalUnit).HasMaxLength(256);
            builder.Property(record => record.MaterialSummary).HasMaxLength(512);
            builder.Property(record => record.SourceResultSetNo).HasMaxLength(64);
            builder.Property(record => record.ForceVoidKind).HasMaxLength(32);
            builder.HasMany(record => record.Items)
                .WithOne(item => item.OutboundRecord)
                .HasForeignKey(item => item.OutboundRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(record => record.SyncEntries)
                .WithOne(entry => entry.OutboundRecord)
                .HasForeignKey(entry => entry.OutboundRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
