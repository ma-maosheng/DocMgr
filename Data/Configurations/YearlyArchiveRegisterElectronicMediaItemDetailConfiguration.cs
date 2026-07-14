using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveRegisterElectronicMediaItemDetailConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterElectronicMediaItemDetail>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterElectronicMediaItemDetail> builder)
        {
            builder.ToTable("YearlyArchiveRegisterElectronicMediaItemDetails");

            builder.HasKey(detail => detail.MediaItemId);

            builder.Property(detail => detail.MaterialCategory)
                .HasMaxLength(50)
                .HasDefaultValue(string.Empty);
            builder.Property(detail => detail.SubCategory)
                .HasMaxLength(100)
                .HasDefaultValue(string.Empty);
            builder.Property(detail => detail.DataOrganizationForm)
                .HasMaxLength(50)
                .HasDefaultValue(string.Empty);
            builder.Property(detail => detail.DataSizeMb)
                .HasPrecision(18, 2);

            builder.HasIndex(detail => detail.MaterialCategory);
            builder.HasIndex(detail => detail.SubCategory);
            builder.HasIndex(detail => detail.DataOrganizationForm);

            builder.HasMany(detail => detail.Entries)
                .WithOne(entry => entry.ElectronicDetail)
                .HasForeignKey(entry => entry.ElectronicMediaItemDetailId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class YearlyArchiveRegisterElectronicMediaItemEntryConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterElectronicMediaItemEntry>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterElectronicMediaItemEntry> builder)
        {
            builder.ToTable("YearlyArchiveRegisterElectronicMediaItemEntries");

            builder.HasKey(entry => entry.Id);

            builder.Property(entry => entry.EntryKind)
                .HasMaxLength(20)
                .HasDefaultValue(string.Empty);
            builder.Property(entry => entry.EntryName)
                .HasMaxLength(500)
                .HasDefaultValue(string.Empty);
            builder.Property(entry => entry.RelativePath)
                .HasMaxLength(1000)
                .HasDefaultValue(string.Empty);
            builder.Property(entry => entry.SizeMb)
                .HasPrecision(18, 2);
            builder.Property(entry => entry.CreatedAt);
            builder.Property(entry => entry.ModifiedAt);

            builder.HasIndex(entry => new { entry.ElectronicMediaItemDetailId, entry.SortOrder });
            builder.HasIndex(entry => entry.EntryName);
        }
    }
}
