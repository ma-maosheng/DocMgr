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
        }
    }
}