using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PLS.Api.Data;

namespace PLS.Api.Tests;

[Collection("Integration")]
public class DataSeederTests
{
    private readonly PlsWebAppFactory _factory;

    public DataSeederTests(PlsWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeedAsync_WithDevSampleFlagDisabled_DoesNotCreateOperationalDemoData()
    {
        await ResetOperationalDataAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: false);

        Assert.Equal(0, await db.OperationScenes.CountAsync());
        Assert.Equal(0, await db.Patients.CountAsync());
        Assert.Equal(0, await db.QrCodePatients.CountAsync(qr => qr.PatientId != null));
        Assert.Equal(0, await db.Bodies.CountAsync());
    }

    [Fact]
    public async Task SeedAsync_WithDevSampleFlagEnabled_CreatesIksSceneAndFiveLinkedPatients()
    {
        await ResetOperationalDataAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: true);

        var scene = await db.OperationScenes.SingleAsync();
        Assert.Equal("IKS", scene.Name);

        var patients = await db.Patients.OrderBy(patient => patient.Id).ToListAsync();
        Assert.Equal(5, patients.Count);
        Assert.Contains(patients, patient => patient.Name == "Anna Berger" && patient.Triagefarbe == "rot");
        Assert.Contains(patients, patient => patient.Name == "Lukas Steiner" && patient.Triagefarbe == "gelb");
        Assert.Contains(patients, patient => patient.Name == "Miriam Huber" && patient.Triagefarbe == "grün");
        Assert.Contains(patients, patient => patient.Name == "Paul Gruber" && patient.Triagefarbe == "blau");
        Assert.Contains(patients, patient => patient.Name == "Unbekannt 5" && patient.Triagefarbe == "schwarz");

        Assert.Equal(5, await db.Bodies.CountAsync());

        var qrCodes = await db.QrCodePatients
            .Where(qr => qr.PatientId != null)
            .OrderBy(qr => qr.QrLogin)
            .Select(qr => new { qr.QrLogin, qr.OperationSceneId })
            .ToListAsync();

        Assert.Equal(5, qrCodes.Count);
        Assert.Equal(
            ["dev-iks-patient-001", "dev-iks-patient-002", "dev-iks-patient-003", "dev-iks-patient-004", "dev-iks-patient-005"],
            qrCodes.Select(qr => qr.QrLogin).ToArray());
        Assert.All(qrCodes, qr => Assert.Equal(scene.Id, qr.OperationSceneId));
    }

    [Fact]
    public async Task SeedAsync_WithDevSampleFlagEnabled_IsIdempotentWhenOperationalDataAlreadyExists()
    {
        await ResetOperationalDataAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: true);

        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: true);

        Assert.Equal(1, await db.OperationScenes.CountAsync());
        Assert.Equal(5, await db.Patients.CountAsync());
        Assert.Equal(5, await db.Bodies.CountAsync());
        Assert.Equal(5, await db.QrCodePatients.CountAsync(qr => qr.PatientId != null));
    }

    [Fact]
    public async Task SeedAsync_SeedsTestUserWithoutRequiresPasswordChange()
    {
        // The "user" role has no self-service change-password flow, so this flag
        // must stay false or the seeded account would be permanently stuck.
        await ResetUsersAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: false,
            seedTestUser: true);

        var testUser = await db.Users.SingleAsync(user => user.Username == PlsWebAppFactory.TestUserUsername);
        Assert.False(testUser.RequiresPasswordChange);

        await ClearAdminPasswordChangeFlagAsync(db);
    }

    [Fact]
    public async Task SeedAsync_WithoutSeedTestUser_DoesNotCreateSharedPresentationAccount()
    {
        // Production runs SeedAsync with seedTestUser: false — the shared demo
        // identity must never exist there (readiness finding C-04).
        await ResetUsersAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: false,
            seedTestUser: false);

        Assert.False(await db.Users.AnyAsync(user => user.Username == PlsWebAppFactory.TestUserUsername));
        Assert.True(await db.Users.AnyAsync(user => user.Username == PlsWebAppFactory.TestAdminUsername));

        // Restore the shared test user so later tests in the collection can log in.
        await DataSeeder.SeedAsync(
            db,
            PlsWebAppFactory.TestAdminUsername,
            PlsWebAppFactory.TestAdminPassword,
            PlsWebAppFactory.TestUserUsername,
            PlsWebAppFactory.TestUserPassword,
            seedDevSampleData: false,
            seedTestUser: true);
        await ClearAdminPasswordChangeFlagAsync(db);
    }

    // A reseeded admin carries RequiresPasswordChange=true, which the API now
    // enforces — later tests in the collection need a fully bootstrapped admin.
    private static async Task ClearAdminPasswordChangeFlagAsync(PlsDbContext db)
    {
        var admin = await db.Users.SingleAsync(user => user.Username == PlsWebAppFactory.TestAdminUsername);
        admin.RequiresPasswordChange = false;
        await db.SaveChangesAsync();
    }

    private async Task ResetUsersAsync()
    {
        await ResetOperationalDataAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        db.Users.RemoveRange(db.Users);
        await db.SaveChangesAsync();
    }

    private async Task ResetOperationalDataAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

        db.AmbulanzprotokollPage1Forms.RemoveRange(db.AmbulanzprotokollPage1Forms);
        db.Bodies.RemoveRange(db.Bodies);
        db.QrCodePatients.RemoveRange(db.QrCodePatients);
        db.Patients.RemoveRange(db.Patients);
        db.OperationScenes.RemoveRange(db.OperationScenes);

        await db.SaveChangesAsync();
    }
}
