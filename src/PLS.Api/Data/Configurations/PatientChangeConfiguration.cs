using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class PatientChangeConfiguration : IEntityTypeConfiguration<PatientChange>
{
    public void Configure(EntityTypeBuilder<PatientChange> builder)
    {
        builder.ToTable("patient_changes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.PatientId).HasColumnName("patient_id");
        builder.Property(c => c.FieldId).HasColumnName("field_id");
        builder.Property(c => c.Value).HasColumnName("value").HasColumnType("json");
        builder.Property(c => c.ChangedAt).HasColumnName("changed_at").HasColumnType("datetime(6)");
        builder.Property(c => c.ReceivedAt).HasColumnName("received_at").HasColumnType("datetime(6)");
        builder.Property(c => c.Accepted).HasColumnName("accepted");
        builder.Property(c => c.Reason).HasColumnName("reason").HasMaxLength(32);
        builder.Property(c => c.ActorId).HasColumnName("actor_id").HasMaxLength(128);
        builder.Property(c => c.DeviceId).HasColumnName("device_id").HasMaxLength(128);

        builder.HasIndex(c => new { c.PatientId, c.FieldId, c.Accepted, c.ChangedAt });
        builder.HasIndex(c => new { c.PatientId, c.Id });
        builder.HasIndex(c => new { c.PatientId, c.FieldId, c.ChangedAt });
        builder.HasOne<Patient>().WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Restrict);
    }
}
