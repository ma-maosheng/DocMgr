using DocMgr.Models.HistoryArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations;

public class HistoryMiscMaterialConfiguration : IEntityTypeConfiguration<HistoryMiscMaterial>
{
    public void Configure(EntityTypeBuilder<HistoryMiscMaterial> builder)
    {
        builder.HasKey(item => item.Id);
        builder.Property(item => item.MajorCategory).HasMaxLength(64).IsRequired();
        builder.Property(item => item.SubCategory).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ContainerKind).HasMaxLength(32).IsRequired();
        builder.Property(item => item.ContainerCode).HasMaxLength(64).IsRequired();
        builder.Property(item => item.StorageLocation).HasMaxLength(64).IsRequired();
        builder.Property(item => item.LastStorageLocation).HasMaxLength(64).IsRequired();
        builder.Property(item => item.LifecycleStatus).HasMaxLength(16).IsRequired();
        builder.Property(item => item.Registrant).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Modifier).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Remark).IsRequired();
        builder.HasIndex(item => item.StorageLocation)
            .IsUnique()
            .HasFilter("[StorageLocation] <> N''");
        builder.HasIndex(item => new { item.ContainerKind, item.ContainerCode })
            .IsUnique()
            .HasFilter("[LifecycleStatus] <> N'已离库'");
        builder.HasMany(item => item.Lines)
            .WithOne(line => line.Material)
            .HasForeignKey(line => line.MaterialId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class HistoryMiscMaterialLineConfiguration : IEntityTypeConfiguration<HistoryMiscMaterialLine>
{
    public void Configure(EntityTypeBuilder<HistoryMiscMaterialLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.LineText).IsRequired();
        builder.HasIndex(line => new { line.MaterialId, line.SortOrder });
    }
}
