using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class TopoMapConfiguration : IEntityTypeConfiguration<TopoMap>
    {
        public void Configure(EntityTypeBuilder<TopoMap> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.Category);
            builder.HasIndex(e => e.Scale);
        }
    }
}

