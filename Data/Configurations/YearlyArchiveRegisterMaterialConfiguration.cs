using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveRegisterMaterialConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterMaterial>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterMaterial> builder)
        {
            builder.HasKey(material => material.Id);
            builder.Property(material => material.MaterialName)
                .HasMaxLength(256)
                .IsRequired();

            // 子项同时级联于介质。这里用 Restrict，避免介质→资料→子项与介质→子项形成两条级联路径。
            builder.HasMany(material => material.Items)
                .WithOne(item => item.RegisterMaterial)
                .HasForeignKey(item => item.YearlyArchiveRegisterMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
