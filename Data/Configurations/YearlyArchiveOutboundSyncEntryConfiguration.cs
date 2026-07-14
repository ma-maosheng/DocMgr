using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveOutboundSyncEntryConfiguration : IEntityTypeConfiguration<YearlyArchiveOutboundSyncEntry>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveOutboundSyncEntry> builder)
        {
            builder.HasKey(entry => entry.Id);
            builder.Property(entry => entry.EntryKind).HasMaxLength(32);
            builder.Property(entry => entry.Phase).HasMaxLength(16);
            builder.Property(entry => entry.OperatedBy).HasMaxLength(64);
            builder.Property(entry => entry.Remark).HasMaxLength(512);
            builder.HasIndex(entry => new { entry.OutboundRecordId, entry.OutboundItemId, entry.EntryKind, entry.Phase });
        }
    }
}
