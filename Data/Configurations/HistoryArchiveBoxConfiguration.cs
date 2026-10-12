using DocMgr.Models.HistoryArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class HistoryArchiveBoxConfiguration : IEntityTypeConfiguration<HistoryArchiveBox>
    {
        public void Configure(EntityTypeBuilder<HistoryArchiveBox> builder)
        {
            builder.HasKey(b => b.Id);
            // 已离库清空盒号后允许多条空串并存；仅非空盒号保持唯一（对齐年度盒销号释放档位）。
            builder.HasIndex(b => b.BoxCode)
                .IsUnique()
                .HasFilter("[BoxCode] <> N''");
            builder.Property(b => b.BoxCode).HasMaxLength(64);
            builder.Property(b => b.BoxSpecification).HasMaxLength(64);
            builder.Property(b => b.PlacementMode).HasMaxLength(32);
            builder.Property(b => b.LastStorageLocation).HasMaxLength(64);
        }
    }

    public class HistoryArchiveBoxLedgerLinkConfiguration : IEntityTypeConfiguration<HistoryArchiveBoxLedgerLink>
    {
        public void Configure(EntityTypeBuilder<HistoryArchiveBoxLedgerLink> builder)
        {
            builder.HasKey(l => l.Id);
            builder.HasIndex(l => new { l.HistoryArchiveBoxId, l.MaterialKind, l.RecordId }).IsUnique();
            builder.HasIndex(l => new { l.MaterialKind, l.RecordId });
            builder.Property(l => l.MaterialKind).HasMaxLength(32);
            builder.HasOne(l => l.Box)
                .WithMany(b => b.LedgerLinks)
                .HasForeignKey(l => l.HistoryArchiveBoxId);
        }
    }
}
