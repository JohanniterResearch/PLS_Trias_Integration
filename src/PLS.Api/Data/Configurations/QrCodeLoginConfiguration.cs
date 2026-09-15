using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class QrCodeLoginConfiguration : IEntityTypeConfiguration<QrCodeLogin>
{
    private const int QrLoginMaxLength = 128;

    public void Configure(EntityTypeBuilder<QrCodeLogin> builder)
    {
        builder.ToTable("qr_code_logins");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");
        builder.Property(q => q.QrLogin)
            .HasColumnName("qr_login")
            .HasMaxLength(QrLoginMaxLength)
            .IsRequired();
        builder.Property(q => q.FirstLogin).HasColumnName("first_login");
        builder.Property(q => q.CreatedAt).HasColumnName("created_at");
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(q => q.QrLogin)
            .IsUnique();
    }
}
