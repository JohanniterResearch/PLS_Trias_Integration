using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class OperationSceneConfiguration : IEntityTypeConfiguration<OperationScene>
{
    public void Configure(EntityTypeBuilder<OperationScene> builder)
    {
        builder.ToTable("operation_scenes");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(o => o.Description).HasColumnName("description");
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
    }
}
