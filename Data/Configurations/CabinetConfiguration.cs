using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class CabinetConfiguration : IEntityTypeConfiguration<Cabinet>
    {
        public void Configure(EntityTypeBuilder<Cabinet> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Ignore(e => e.IsSelected);
            builder.Property(e => e.HardDiskSlotCapacity)
                .HasDefaultValue(CabinetHardDiskSlotCategoryAssignment.DedicatedHardDiskSlotCapacity);
            builder.Property(e => e.OpticalDiscSlotCapacity)
                .HasDefaultValue(CabinetHardDiskSlotCategoryAssignment.DedicatedOpticalDiscSlotCapacity);
        }
    }
}