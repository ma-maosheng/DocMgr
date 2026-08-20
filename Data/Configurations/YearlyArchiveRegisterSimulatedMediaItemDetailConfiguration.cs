using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveRegisterSimulatedMediaItemDetailConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterSimulatedMediaItemDetail>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterSimulatedMediaItemDetail> builder)
        {
            builder.ToTable("YearlyArchiveRegisterSimulatedMediaItemDetails");

            builder.HasKey(detail => detail.MediaItemId);

            builder.Property(detail => detail.MaterialCategory)
                .HasMaxLength(50)
                .HasDefaultValue(string.Empty);
            builder.Property(detail => detail.SubCategory)
                .HasMaxLength(100)
                .HasDefaultValue(string.Empty);
            builder.Property(detail => detail.OrganizationForm)
                .HasMaxLength(50)
                .HasDefaultValue(string.Empty);

            builder.HasIndex(detail => detail.MaterialCategory);
            builder.HasIndex(detail => detail.SubCategory);
            builder.HasIndex(detail => detail.OrganizationForm);
        }
    }
}
