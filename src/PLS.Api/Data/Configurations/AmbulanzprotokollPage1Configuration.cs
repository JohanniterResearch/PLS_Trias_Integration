using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class AmbulanzprotokollPage1Configuration : IEntityTypeConfiguration<AmbulanzprotokollPage1>
{
    public void Configure(EntityTypeBuilder<AmbulanzprotokollPage1> builder)
    {
        builder.ToTable("patient_ambulanzprotokoll_page1");
        builder.HasKey(page => page.Id);
        builder.Property(page => page.Id).HasColumnName("id");
        builder.Property(page => page.PatientId).HasColumnName("patient_id");
        builder.Property(page => page.FormStateJson)
            .HasColumnName("form_state")
            .HasColumnType("json");
        builder.Property(page => page.Status)
            .HasColumnName("status")
            .HasMaxLength(32);
        builder.Property(page => page.Revision).HasColumnName("revision").HasDefaultValue(0);
        builder.Property(page => page.FinalizedAt).HasColumnName("finalized_at");
        builder.Property(page => page.CreatedAt).HasColumnName("created_at");
        builder.Property(page => page.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(page => page.PatientId)
            .IsUnique();

        builder.HasOne(page => page.Patient)
            .WithOne(patient => patient.AmbulanzprotokollPage1)
            .HasForeignKey<AmbulanzprotokollPage1>(page => page.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
