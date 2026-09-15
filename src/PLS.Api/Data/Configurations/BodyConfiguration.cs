using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class BodyConfiguration : IEntityTypeConfiguration<Body>
{
    public void Configure(EntityTypeBuilder<Body> builder)
    {
        builder.ToTable("bodies");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.IdPatient).HasColumnName("id_patient");
        builder.Property(b => b.BodyParts)
            .HasColumnName("body_parts")
            .HasColumnType("json")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, (JsonSerializerOptions?)null)!,
                new ValueComparer<Dictionary<string, int>>(
                    (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                    v => JsonSerializer.Deserialize<Dictionary<string, int>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!));
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(b => b.Patient)
            .WithOne(p => p.Body)
            .HasForeignKey<Body>(b => b.IdPatient);
    }
}
