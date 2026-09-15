using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_persons_triagefarbe_valid",
                "triagefarbe IS NULL OR triagefarbe IN ('rot','gelb','grün','blau','schwarz')"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Triagefarbe).HasColumnName("triagefarbe").HasMaxLength(16);
        builder.Property(p => p.Position).HasColumnName("position").HasColumnType("POINT");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
    }
}
