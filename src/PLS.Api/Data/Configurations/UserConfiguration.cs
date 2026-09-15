using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(255).IsRequired();
        builder.Property(u => u.Password).HasColumnName("password").IsRequired();
        builder.Property(u => u.AdminRole).HasColumnName("admin_role").HasDefaultValue(false);
        builder.Property(u => u.LongitudeUser).HasColumnName("longitude_user");
        builder.Property(u => u.LatitudeUser).HasColumnName("latitude_user");
        builder.Property(u => u.FirstLoginTime).HasColumnName("first_login_time");
        builder.Property(u => u.LastLoginTime).HasColumnName("last_login_time");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Username).IsUnique();
    }
}
