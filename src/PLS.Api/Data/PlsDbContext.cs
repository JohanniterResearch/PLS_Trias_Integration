using Microsoft.EntityFrameworkCore;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data;

public class PlsDbContext : DbContext
{
    public PlsDbContext(DbContextOptions<PlsDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<AmbulanzprotokollPage1> AmbulanzprotokollPage1Forms => Set<AmbulanzprotokollPage1>();
    public DbSet<Body> Bodies => Set<Body>();
    public DbSet<QrCodeLogin> QrCodeLogins => Set<QrCodeLogin>();
    public DbSet<QrCodePatient> QrCodePatients => Set<QrCodePatient>();
    public DbSet<OperationScene> OperationScenes => Set<OperationScene>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<QrCodeLoginHasUser> QrCodeLoginHasUsers => Set<QrCodeLoginHasUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlsDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            var now = DateTime.UtcNow;

            if (entry.Metadata.FindProperty("UpdatedAt") != null)
                entry.Property("UpdatedAt").CurrentValue = now;

            if (entry.State == EntityState.Added && entry.Metadata.FindProperty("CreatedAt") != null)
                entry.Property("CreatedAt").CurrentValue = now;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
