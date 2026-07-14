using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveOutboundItemConfiguration : IEntityTypeConfiguration<YearlyArchiveOutboundItem>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveOutboundItem> builder)
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.ArchiveCopyRole).HasMaxLength(16);
            builder.Property(item => item.SelectionScopeKind).HasMaxLength(32);
            builder.Property(item => item.FormNo).HasMaxLength(64);
            builder.Property(item => item.MaterialName).HasMaxLength(256);
            builder.Property(item => item.ItemName).HasMaxLength(256);
            builder.Property(item => item.ContainerCode).HasMaxLength(64);
            builder.Property(item => item.StorageLocation).HasMaxLength(128);
            builder.Property(item => item.ConfidentialLevel).HasMaxLength(32);
            builder.Property(item => item.ArchivePurpose).HasMaxLength(64);
            builder.Property(item => item.MediaKind).HasMaxLength(16);
            builder.Property(item => item.MediaType).HasMaxLength(64);
            builder.Property(item => item.StorageCarrierType).HasMaxLength(64);
            builder.Property(item => item.UsageMode).HasMaxLength(32);
            builder.Property(item => item.ElectronicMediaSource).HasMaxLength(32);
            builder.Property(item => item.ElectronicMediumType).HasMaxLength(32);
            builder.Property(item => item.ItemProjectName).HasMaxLength(256);
            builder.Property(item => item.RequisitionedDiskCode).HasMaxLength(64);
            builder.Property(item => item.ReservationStatus).HasMaxLength(16);
            builder.Property(item => item.ContainerStatusHint).HasMaxLength(32);
            builder.Property(item => item.CurrentStorageLocation).HasMaxLength(128);
        }
    }
}
