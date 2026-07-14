using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
    {
        public void Configure(EntityTypeBuilder<UserSession> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.SessionId)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(e => e.TerminalName)
                .HasMaxLength(128)
                .IsRequired();

            builder.HasIndex(e => e.SessionId)
                .IsUnique();

            builder.HasIndex(e => e.UserId)
                .IsUnique()
                .HasFilter("\"IsActive\" = 1");

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
