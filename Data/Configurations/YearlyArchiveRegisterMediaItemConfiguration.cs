using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveRegisterMediaItemConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterMediaItem>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterMediaItem> builder)
        {
            builder.HasKey(item => item.Id);

            builder.Property(item => item.SourceType)
                .HasMaxLength(64);

            builder.Property(item => item.ProvideUnit)
                .HasMaxLength(256);

            builder.HasOne(item => item.ElectronicDetail)
                .WithOne(detail => detail.MediaItem)
                .HasForeignKey<YearlyArchiveRegisterElectronicMediaItemDetail>(detail => detail.MediaItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(item => item.SimulatedDetail)
                .WithOne(detail => detail.MediaItem)
                .HasForeignKey<YearlyArchiveRegisterSimulatedMediaItemDetail>(detail => detail.MediaItemId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
