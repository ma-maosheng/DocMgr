using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class AerialPhotoConfiguration : IEntityTypeConfiguration<AerialPhoto>
    {
        public void Configure(EntityTypeBuilder<AerialPhoto> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.Category);
        }
    }
}