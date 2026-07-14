using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocMgr.Data.Configurations
{
    public class FieldDomainDefinitionConfiguration : IEntityTypeConfiguration<FieldDomainDefinition>
    {
        public void Configure(EntityTypeBuilder<FieldDomainDefinition> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.EntityName).IsRequired();
            builder.Property(e => e.FieldName).IsRequired();
            builder.Property(e => e.DisplayName).IsRequired();
            builder.Property(e => e.Description).IsRequired();

            builder.HasIndex(e => new { e.EntityName, e.FieldName }).IsUnique();

            builder.HasMany(e => e.Options)
                .WithOne(o => o.FieldDefinition)
                .HasForeignKey(o => o.FieldDomainDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
