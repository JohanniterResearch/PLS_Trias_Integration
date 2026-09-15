using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PLS.Api.Data;
using Testcontainers.MySql;

namespace PLS.Api.Tests;

/// <summary>
/// Spins up the full ASP.NET Core pipeline with a real MySQL container.
/// MQTT is disabled via configuration so no broker is needed.
/// Test credentials are injected via host configuration (no process-wide env var mutation).
/// </summary>
public class PlsWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Test-only credentials — independent of production defaults.
    // All test suites must use these constants instead of hardcoding credential strings.
    public const string TestAdminUsername = "test-admin";
    public const string TestAdminPassword = "test-admin-password-for-ci";
    public const string TestUserUsername = "test-user";
    public const string TestUserPassword = "test-user-password-for-ci";
    public const string TestJwtSecret = "test-jwt-secret-that-is-at-least-32-characters-long!";

    private readonly MySqlContainer _mysql = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .Build();

    public async Task InitializeAsync()
    {
        await _mysql.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace DbContext with the test container connection string
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PlsDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            services.AddDbContext<PlsDbContext>(options =>
                options.UseMySql(
                    _mysql.GetConnectionString(),
                    new MySqlServerVersion(new Version(8, 0, 0)),
                    o => o.UseNetTopologySuite().EnableRetryOnFailure()));
        });

        // Inject test-only bootstrap credentials and disable MQTT.
        // Use web host settings so values are available during Program startup configuration reads.
        builder.UseSetting("Jwt:Secret", TestJwtSecret);
        builder.UseSetting("Mqtt:Enabled", "false");
        builder.UseSetting("Bootstrap:AdminUsername", TestAdminUsername);
        builder.UseSetting("Bootstrap:AdminPassword", TestAdminPassword);
        builder.UseSetting("Bootstrap:TestUserUsername", TestUserUsername);
        builder.UseSetting("Bootstrap:TestUserPassword", TestUserPassword);
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // The seeder marks the bootstrap admin with RequiresPasswordChange, which the
        // API now enforces as an authorization state. Tests model an admin who has
        // completed the initial password change.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
        var admin = db.Users.SingleOrDefault(u => u.Username == TestAdminUsername);
        if (admin != null && admin.RequiresPasswordChange)
        {
            admin.RequiresPasswordChange = false;
            db.SaveChanges();
        }

        return host;
    }

    public new async Task DisposeAsync()
    {
        await _mysql.DisposeAsync();
    }
}
