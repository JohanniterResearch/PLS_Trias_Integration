using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients", tableBuilder =>
            tableBuilder.HasCheckConstraint(
                "CK_patients_triagefarbe_valid",
                "triagefarbe IS NULL OR triagefarbe IN ('rot','gelb','grün','blau','schwarz')"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Atmung).HasColumnName("atmung");
        builder.Property(p => p.Blutung).HasColumnName("blutung");
        builder.Property(p => p.Radialispuls).HasColumnName("radialispuls");
        builder.Property(p => p.Triagefarbe).HasColumnName("triagefarbe").HasMaxLength(16);
        builder.Property(p => p.Transport).HasColumnName("transport");
        builder.Property(p => p.Dringend).HasColumnName("dringend");
        builder.Property(p => p.Kontaminiert).HasColumnName("kontaminiert");
        builder.Property(p => p.Name).HasColumnName("name");
        builder.Property(p => p.LongitudePatient).HasColumnName("longitude_patient");
        builder.Property(p => p.LatitudePatient).HasColumnName("latitude_patient");
        builder.Property(p => p.UserIdUser).HasColumnName("user_id_user");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserIdUser)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
