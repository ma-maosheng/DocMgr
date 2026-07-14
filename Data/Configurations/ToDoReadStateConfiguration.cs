using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace DocMgr.Data.Configurations
{
    public class ToDoReadStateConfiguration : IEntityTypeConfiguration<ToDoReadState>
    {
        public void Configure(EntityTypeBuilder<ToDoReadState> builder)
        {
            builder.HasKey(x => x.Id); builder.HasIndex(x => new { x.UserId, x.ToDoId }).IsUnique();
        }
    }
}
