using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DocMgr.Data.Configurations
{
    public class ProjectInfoConfiguration : IEntityTypeConfiguration<ProjectInfo>
    {
        public void Configure(EntityTypeBuilder<ProjectInfo> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => e.ProjectCode);
        }
    }
}
