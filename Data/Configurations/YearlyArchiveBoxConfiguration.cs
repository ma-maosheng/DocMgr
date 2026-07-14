using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveBoxConfiguration : IEntityTypeConfiguration<YearlyArchiveBox>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveBox> builder)
        {
            builder.HasKey(b => b.Id); builder.HasIndex(b => b.ArchiveSequenceNo).IsUnique(); // 档案编号唯一
        }
    }
}
