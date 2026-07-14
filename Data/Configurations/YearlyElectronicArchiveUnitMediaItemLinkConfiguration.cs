using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyElectronicArchiveUnitMediaItemLinkConfiguration
        : IEntityTypeConfiguration<YearlyElectronicArchiveUnitMediaItemLink>
    {
        public void Configure(EntityTypeBuilder<YearlyElectronicArchiveUnitMediaItemLink> builder)
        {
            builder.ToTable("YearlyElectronicArchiveUnitMediaItemLinks");
            builder.HasKey(link => link.Id);

            builder.Property(link => link.FilingStoragePath).HasMaxLength(2000);
            builder.Property(link => link.MediumCode).HasMaxLength(128);
            builder.Property(link => link.FormNo).HasMaxLength(64);
            builder.Property(link => link.MaterialName).HasMaxLength(256);
            builder.Property(link => link.ItemName)
                .HasColumnName("ContentSummary")
                .HasMaxLength(2000);

            builder.HasIndex(link => new { link.YearlyElectronicArchiveUnitId, link.YearlyArchiveRegisterMediaItemId })
                .IsUnique();
            builder.HasIndex(link => link.YearlyArchiveRegisterMediaItemId)
                .IsUnique();
            builder.HasIndex(link => link.MediumCode);
            builder.HasIndex(link => link.FormNo);
        }
    }
}
