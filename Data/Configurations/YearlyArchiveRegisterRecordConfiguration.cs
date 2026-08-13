using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveRegisterRecordConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterRecord>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterRecord> builder)
        {
            builder.HasKey(r => r.Id); builder.HasIndex(r => r.FormNo).IsUnique();
            builder.HasIndex(r => r.SourceNetworkOutboundRecordId)
                .IsUnique()
                .HasFilter("[SourceNetworkOutboundRecordId] IS NOT NULL");
            builder.HasIndex(r => r.BusinessChainId);
            // 显式忽略非映射字段，避免旧列配置残留
            builder.Ignore(r => r.ArchiveBoxNos);
            builder.Ignore(r => r.ArchiveBoxLocations);
            builder.Ignore(r => r.StatusStr);
            builder.Ignore(r => r.StatusColor);

            // 一对多：登记记录 -> 介质条目（级联删除）
            builder.HasMany(r => r.MediaEntries)
                .WithOne()
                .HasForeignKey(m => m.YearlyArchiveRegisterRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            // 多对多：登记记录 <-> 档案盒
            builder.HasMany(r => r.ArchiveBoxes)
                .WithMany(b => b.RegisterRecords);

            builder.HasOne(r => r.BusinessChain)
                .WithMany()
                .HasForeignKey(r => r.BusinessChainId)
                .OnDelete(DeleteBehavior.SetNull);

        }
    }
}