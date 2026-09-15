using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class QrCodePatientConfiguration : IEntityTypeConfiguration<QrCodePatient>
{
    private const int QrLoginMaxLength = 128;

    public void Configure(EntityTypeBuilder<QrCodePatient> builder)
    {
        builder.ToTable("qr_code_patients");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.QrLogin)
            .HasColumnName("qr_login")
            .HasMaxLength(QrLoginMaxLength)
            .IsRequired();
        builder.Property(q => q.PatientId).HasColumnName("patient_id");
        builder.Property(q => q.UserId).HasColumnName("user_id");
        builder.Property(q => q.OperationSceneId).HasColumnName("operation_scene_id");
        builder.Property(q => q.CreatedAt).HasColumnName("created_at");
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(q => q.QrLogin)
            .IsUnique();

        builder.HasOne(q => q.Patient)
            .WithOne(p => p.QrCodePatient)
            .HasForeignKey<QrCodePatient>(q => q.PatientId);

        builder.HasOne(q => q.User)
            .WithMany()
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(q => q.OperationScene)
            .WithMany()
            .HasForeignKey(q => q.OperationSceneId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
