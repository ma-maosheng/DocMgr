using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveRelocationItemConfiguration : IEntityTypeConfiguration<YearlyArchiveRelocationItem>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRelocationItem> builder)
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.SourceLinkType).HasMaxLength(64);
            builder.Property(item => item.BeforeContainerCode).HasMaxLength(64);
            builder.Property(item => item.BeforeStorageLocation).HasMaxLength(128);
            builder.Property(item => item.AfterContainerCode).HasMaxLength(64);
            builder.Property(item => item.AfterStorageLocation).HasMaxLength(128);
        }
    }
}
