using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveFilingFactConfiguration : IEntityTypeConfiguration<YearlyArchiveFilingFact>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveFilingFact> builder)
        {
            builder.ToTable("YearlyArchiveFilingFacts");
            builder.HasKey(fact => fact.Id);

            builder.Property(fact => fact.FilingFactNo).HasMaxLength(32).IsRequired();
            builder.Property(fact => fact.MediaKind).HasMaxLength(8).IsRequired();
            builder.Property(fact => fact.FormNo).HasMaxLength(64);
            builder.Property(fact => fact.MaterialName).HasMaxLength(256);
            builder.Property(fact => fact.ProjectName).HasMaxLength(256);
            builder.Property(fact => fact.ProvideUnit).HasMaxLength(128);
            builder.Property(fact => fact.ApplicantName).HasMaxLength(64);
            builder.Property(fact => fact.ItemType).HasMaxLength(16);
            builder.Property(fact => fact.ItemName).HasMaxLength(512);
            builder.Property(fact => fact.ContainerCode).HasMaxLength(64);
            builder.Property(fact => fact.StorageLocation).HasMaxLength(128);
            builder.Property(fact => fact.CabinetName).HasMaxLength(64);
            builder.Property(fact => fact.BoxLocationCode).HasMaxLength(64);
            builder.Property(fact => fact.BoxSpecs).HasMaxLength(16);
            builder.Property(fact => fact.StorageCarrierType).HasMaxLength(32);
            builder.Property(fact => fact.Disposition).HasMaxLength(32);
            builder.Property(fact => fact.MediumCode).HasMaxLength(64);
            builder.Property(fact => fact.FilingStoragePath).HasMaxLength(512);
            builder.Property(fact => fact.FiledBy).HasMaxLength(64);
            builder.Property(fact => fact.SourceLinkType).HasMaxLength(32).IsRequired();
            builder.Property(fact => fact.LifecycleStatus).HasMaxLength(16);
            builder.Property(fact => fact.CurrentContainerCode).HasMaxLength(64);
            builder.Property(fact => fact.CurrentStorageLocation).HasMaxLength(128);
            builder.Property(fact => fact.LifecycleRemark).HasMaxLength(512);
            builder.Property(fact => fact.BorrowHintLevel).HasMaxLength(16);
            builder.Property(fact => fact.BorrowHintText).HasMaxLength(256);
            builder.Property(fact => fact.ArchiveCopyRole).HasMaxLength(16);

            builder.HasIndex(fact => new { fact.SourceLinkType, fact.SourceLinkId }).IsUnique();
            builder.HasIndex(fact => fact.PrimaryFilingFactId);
            builder.HasIndex(fact => new { fact.MediaKind, fact.FiledAt });
            builder.HasIndex(fact => fact.FormNo);
            builder.HasIndex(fact => fact.ProjectName);
            builder.HasIndex(fact => fact.ContainerCode);
            builder.HasIndex(fact => fact.StorageLocation);
            builder.HasIndex(fact => fact.MediumCode);
            builder.HasIndex(fact => fact.BoxLocationCode);
            builder.HasIndex(fact => fact.LifecycleStatus);
            builder.HasIndex(fact => fact.MediaItemId);
        }
    }
}
