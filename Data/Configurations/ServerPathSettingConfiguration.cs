using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class ServerPathSettingConfiguration : IEntityTypeConfiguration<ServerPathSetting>
    {
        public void Configure(EntityTypeBuilder<ServerPathSetting> builder)
        {
            builder.HasKey(e => e.Id);
            builder.HasIndex(e => new { e.DepartmentName, e.PathName }).IsUnique();
            builder.Property(e => e.DepartmentName).HasMaxLength(100);
            builder.Property(e => e.PathName).HasMaxLength(500);
            builder.Property(e => e.PhysicalPath).HasMaxLength(1000);
            builder.Property(e => e.Permission).HasMaxLength(20);
            builder.Property(e => e.CapacityTb).HasPrecision(18, 4);
        }
    }
}
