using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PLS.Api.Data;

namespace PLS.Api.Tests;

/// <summary>
/// Runs the full pipeline with ASPNETCORE_ENVIRONMENT=Production and deliberately
/// misconfigured dev flags: dev endpoints and dev seeding must stay disabled anyway.
/// </summary>
public class PlsProductionWebAppFactory : PlsWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
        // Production never auto-migrates; the test container has no schema yet.
        // Process-wide, but harmless for the dev factory (Development migrates anyway).
        Environment.SetEnvironmentVariable("PLS_AUTO_MIGRATE", "true");
        // Worst case: every dev convenience flag is accidentally on in production.
        builder.UseSetting("Features:EnableDevLogin", "true");
        builder.UseSetting("Features:EnableDevQrHelper", "true");
        builder.UseSetting("Bootstrap:SeedDevSampleData", "true");
    }
}

[Collection("ProductionIntegration")]
public class ProductionEnvironmentTests
{
    private readonly PlsProductionWebAppFactory _factory;
    private readonly HttpClient _client;

    public ProductionEnvironmentTests(PlsProductionWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DevLogin_InProduction_Returns404_EvenWithFlagEnabled()
    {
        var response = await _client.PostAsJsonAsync("/api/dev-login", new { role = "admin" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevUnusedPatientQrCodes_InProduction_Returns404_EvenWithFlagEnabled()
    {
        var adminToken = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dev/unusedPatientQrCodes");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SharedPresentationAccount_DoesNotExistInProduction()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Username == PlsWebAppFactory.TestUserUsername));

        var login = await _client.PostAsJsonAsync("/api/user-login",
            new { username = PlsWebAppFactory.TestUserUsername, password = PlsWebAppFactory.TestUserPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task DevSampleData_IsNotSeededInProduction_EvenWithFlagEnabled()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
        Assert.False(await db.OperationScenes.AnyAsync(s => s.Name == "IKS"));
        Assert.False(await db.Patients.AnyAsync());
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return data!["token"].ToString()!;
    }
}

[CollectionDefinition("ProductionIntegration")]
public class ProductionIntegrationCollection : ICollectionFixture<PlsProductionWebAppFactory>;
