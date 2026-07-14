using DocMgr.Models.SystemSettings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class BusinessLogicSettingsConfiguration : IEntityTypeConfiguration<BusinessLogicSettings>
    {
        public void Configure(EntityTypeBuilder<BusinessLogicSettings> builder)
        {
            builder.HasKey(item => item.Id);
            builder.Property(item => item.ApplicationOverdueSetting).HasMaxLength(32).IsRequired();
        }
    }
}
