using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DocMgr.Data.Configurations
{
    public class YearlyArchiveRegisterMediaConfiguration : IEntityTypeConfiguration<YearlyArchiveRegisterMedia>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveRegisterMedia> builder)
        {
            builder.HasKey(m => m.Id);
            builder.Property(m => m.BorrowedHardDiskCode)
                .HasMaxLength(100)
                .HasDefaultValue(string.Empty);
            builder.Property(m => m.RequisitionedHardDiskCode)
                .HasMaxLength(64)
                .HasDefaultValue(string.Empty);
            // 一对多：介质条目 -> 介质明细（级联删除）
            builder.HasMany(m => m.Items)
                .WithOne()
                .HasForeignKey(i => i.YearlyArchiveRegisterMediaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
