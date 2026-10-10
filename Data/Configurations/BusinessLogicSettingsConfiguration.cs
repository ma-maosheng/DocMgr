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
            // 单例行由数据库生成 Id（首行通常为 1）；禁止客户端写入显式标识值。
            builder.Property(item => item.Id).ValueGeneratedOnAdd();
            builder.Property(item => item.ApplicationOverdueSetting).HasMaxLength(32).IsRequired();
        }
    }
}
