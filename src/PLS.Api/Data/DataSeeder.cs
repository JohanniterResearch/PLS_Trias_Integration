using Microsoft.EntityFrameworkCore;
using PLS.Api.Models.Entities;

namespace PLS.Api.Data;

public static class DataSeeder
{
    private static readonly DevPatientSeed[] DevPatients =
    [
        new("dev-iks-patient-001", "Anna Berger", true, true, "rot", true, true, false, 48.2721, 16.4021),
        new("dev-iks-patient-002", "Lukas Steiner", true, false, "gelb", true, true, false, 48.2708, 16.4014),
        new("dev-iks-patient-003", "Miriam Huber", true, false, "grün", false, false, false, 48.2719, 16.4042),
        new("dev-iks-patient-004", "Paul Gruber", false, false, "blau", true, true, true, 48.2728, 16.4048),
        new("dev-iks-patient-005", "Unbekannt 5", false, true, "schwarz", false, false, false, 48.2706, 16.4035),
    ];

    public static async Task SeedAsync(
        PlsDbContext context,
        string adminUsername,
        string adminPassword,
        string testUserUsername,
        string testUserPassword,
        bool seedDevSampleData = false,
        bool seedTestUser = false,
        bool requireAdminPasswordChange = true)
    {
        if (!await context.Users.AnyAsync())
        {
            context.Users.Add(
                new User
                {
                    Username = adminUsername,
                    Password = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                    AdminRole = true,
                    // Dev/test bootstrap admin skips the forced change (nobody rotates
                    // the "admin"/"admin" default outside production); prod always forces it.
                    RequiresPasswordChange = requireAdminPasswordChange
                }
            );
            await context.SaveChangesAsync();
        }

        // The shared test/presentation account is a dev-only convenience (used by the
        // dev-login endpoint). Production deployments must never create it.
        if (seedTestUser
            && !string.IsNullOrEmpty(testUserUsername)
            && !string.IsNullOrEmpty(testUserPassword)
            && !await context.Users.AnyAsync(u => u.Username == testUserUsername))
        {
            context.Users.Add(new User
            {
                // ponytail: RequiresPasswordChange stays false because the "user" role has no
                // self-service change-password flow — setting it true would strand the account.
                Username = testUserUsername,
                Password = BCrypt.Net.BCrypt.HashPassword(testUserPassword),
                AdminRole = false,
                RequiresPasswordChange = false
            });
            await context.SaveChangesAsync();
        }

        if (!seedDevSampleData || !await ShouldSeedDevSampleDataAsync(context))
            return;

        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync();

            if (!await ShouldSeedDevSampleDataAsync(context))
            {
                await transaction.RollbackAsync();
                return;
            }

            var scene = new OperationScene
            {
                Name = "IKS",
                Description = "Dev seed scene"
            };
            context.OperationScenes.Add(scene);
            await context.SaveChangesAsync();

            foreach (var patientSeed in DevPatients)
            {
                var patient = new Patient
                {
                    Atmung = patientSeed.Atmung,
                    Blutung = patientSeed.Blutung,
                    Triagefarbe = patientSeed.Triagefarbe,
                    Transport = patientSeed.Transport,
                    Dringend = patientSeed.Dringend,
                    Kontaminiert = patientSeed.Kontaminiert,
                    Name = patientSeed.Name,
                    LatitudePatient = patientSeed.Latitude,
                    LongitudePatient = patientSeed.Longitude
                };
                context.Patients.Add(patient);
                await context.SaveChangesAsync();

                context.Bodies.Add(new Body
                {
                    IdPatient = patient.Id,
                    BodyParts = BodyPartConstants.CreateDefault()
                });

                context.QrCodePatients.Add(new QrCodePatient
                {
                    QrLogin = patientSeed.QrCode,
                    PatientId = patient.Id,
                    OperationSceneId = scene.Id
                });
            }

            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        });
    }

    private static async Task<bool> ShouldSeedDevSampleDataAsync(PlsDbContext context)
    {
        var hasOperationScenes = await context.OperationScenes.AnyAsync();
        var hasPatients = await context.Patients.AnyAsync();
        var hasLinkedQrPatients = await context.QrCodePatients.AnyAsync(qr => qr.PatientId != null);

        return !hasOperationScenes && !hasPatients && !hasLinkedQrPatients;
    }

    private sealed record DevPatientSeed(
        string QrCode,
        string Name,
        bool Atmung,
        bool Blutung,
        string Triagefarbe,
        bool Transport,
        bool Dringend,
        bool Kontaminiert,
        double Latitude,
        double Longitude);
}
