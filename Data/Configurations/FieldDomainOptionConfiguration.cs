using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class FieldDomainOptionConfiguration : IEntityTypeConfiguration<FieldDomainOption>
    {
        public void Configure(EntityTypeBuilder<FieldDomainOption> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.Scope).IsRequired();
            builder.Property(e => e.OptionValue).IsRequired();
            builder.Property(e => e.OptionLabel).IsRequired();

            builder.HasIndex(e => new { e.FieldDomainDefinitionId, e.Scope, e.OptionValue }).IsUnique();
        }
    }
}
