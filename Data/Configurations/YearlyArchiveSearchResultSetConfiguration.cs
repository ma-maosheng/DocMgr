using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveSearchResultSetConfiguration : IEntityTypeConfiguration<YearlyArchiveSearchResultSet>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveSearchResultSet> builder)
        {
            builder.ToTable("YearlyArchiveSearchResultSets");
            builder.HasKey(set => set.Id);

            builder.Property(set => set.ResultSetNo).HasMaxLength(32).IsRequired();
            builder.Property(set => set.Name).HasMaxLength(128).IsRequired();
            builder.Property(set => set.MediaKind).HasMaxLength(8).IsRequired();
            builder.Property(set => set.Status).HasMaxLength(16);
            builder.Property(set => set.CreatedByName).HasMaxLength(64);
            builder.Property(set => set.Remarks).HasMaxLength(512);

            builder.HasMany(set => set.Items)
                .WithOne(item => item.ResultSet)
                .HasForeignKey(item => item.ResultSetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(set => new { set.MediaKind, set.CreatedAt });
            builder.HasIndex(set => set.CreatedByUserId);
        }
    }

    public sealed class YearlyArchiveSearchResultSetItemConfiguration : IEntityTypeConfiguration<YearlyArchiveSearchResultSetItem>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveSearchResultSetItem> builder)
        {
            builder.ToTable("YearlyArchiveSearchResultSetItems");
            builder.HasKey(item => item.Id);

            builder.Property(item => item.FormNo).HasMaxLength(64);
            builder.Property(item => item.MaterialName).HasMaxLength(256);
            builder.Property(item => item.ItemName).HasMaxLength(512);
            builder.Property(item => item.ContainerCode).HasMaxLength(64);
            builder.Property(item => item.StorageLocation).HasMaxLength(128);
            builder.Property(item => item.LifecycleStatus).HasMaxLength(16);
            builder.Property(item => item.BorrowHintLevel).HasMaxLength(16);
            builder.Property(item => item.BorrowHintText).HasMaxLength(256);
            builder.Property(item => item.SelectionScopeKind).HasMaxLength(32).IsRequired();
            builder.Property(item => item.ContentEntryKind).HasMaxLength(16);
            builder.Property(item => item.ContentEntryName).HasMaxLength(512);
            builder.Property(item => item.ContentEntryRelativePath).HasMaxLength(1024);

            builder.HasIndex(item => new { item.ResultSetId, item.FilingFactId, item.SelectionScopeKind, item.ContentEntryId })
                .IsUnique();
        }
    }
}
