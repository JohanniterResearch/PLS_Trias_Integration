using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class QrCodeLoginHasUserConfiguration : IEntityTypeConfiguration<QrCodeLoginHasUser>
{
    public void Configure(EntityTypeBuilder<QrCodeLoginHasUser> builder)
    {
        builder.ToTable("qr_code_login_has_users");
        builder.HasKey(x => new { x.QrCodeLoginId, x.UserId });
        builder.Property(x => x.QrCodeLoginId).HasColumnName("qr_code_login_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.HasOne(x => x.QrCodeLogin).WithMany().HasForeignKey(x => x.QrCodeLoginId);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}
