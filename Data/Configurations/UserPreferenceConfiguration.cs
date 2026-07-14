using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DocMgr.Data.Configurations
{
    public class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
    {
        public void Configure(EntityTypeBuilder<UserPreference> builder)
        {
            builder.HasKey(x => x.Id); builder.HasIndex(x => x.UserId).IsUnique();
        }
    }
}