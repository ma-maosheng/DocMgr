using DocMgr.Models.YearlyArchive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public sealed class YearlyArchiveReturnRecordConfiguration : IEntityTypeConfiguration<YearlyArchiveReturnRecord>
    {
        public void Configure(EntityTypeBuilder<YearlyArchiveReturnRecord> builder)
        {
            builder.HasKey(record => record.Id);
            builder.HasIndex(record => record.ReturnNo).IsUnique();
            builder.HasIndex(record => record.SourceOutboundRecordId);
            builder.Property(record => record.ReturnNo).HasMaxLength(64);
            builder.Property(record => record.SourceOutboundNo).HasMaxLength(64);
            builder.Property(record => record.ProjectName).HasMaxLength(256);
            builder.Property(record => record.BorrowerName).HasMaxLength(64);
            builder.Property(record => record.BorrowerDept).HasMaxLength(128);
            builder.Property(record => record.RegisteredByName).HasMaxLength(64);
            builder.Property(record => record.RegisteredByDept).HasMaxLength(128);
            builder.Property(record => record.Reason).HasMaxLength(512);
            builder.Property(record => record.Remark).HasMaxLength(512);
            builder.Property(record => record.LossDescription).HasMaxLength(1024);
            builder.Property(record => record.HandlerName).HasMaxLength(64);
            builder.Property(record => record.ReviewerName).HasMaxLength(64);
            builder.Property(record => record.ApprovedBy).HasMaxLength(64);
            builder.Property(record => record.ApprovalOpinion).HasMaxLength(512);
            builder.Property(record => record.ProductionHead).HasMaxLength(64);
            builder.Property(record => record.VicePresident).HasMaxLength(64);
            builder.Property(record => record.HandoverApplicant).HasMaxLength(64);
            builder.Property(record => record.HandoverAdmin).HasMaxLength(64);
            builder.Property(record => record.SignedAttachmentUploader).HasMaxLength(64);
            builder.Property(record => record.VoidReason).HasMaxLength(512);
            builder.Property(record => record.ForceVoidReason).HasMaxLength(512);
            builder.HasMany(record => record.Items)
                .WithOne(item => item.ReturnRecord)
                .HasForeignKey(item => item.ReturnRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
