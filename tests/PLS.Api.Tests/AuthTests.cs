using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PLS.Api.Data;

namespace PLS.Api.Tests;

[Collection("Integration")]
public class AuthTests
{
    private readonly PlsWebAppFactory _factory;
    private readonly HttpClient _client;

    public AuthTests(PlsWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Admin login ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AdminLogin_WithTestCredentials_ReturnsSuccessAndToken()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();

        Assert.NotNull(data);
        Assert.Equal("success", data["status"].ToString()!.ToLower());
        Assert.True(data.ContainsKey("token"));
        Assert.NotEmpty(data["token"].ToString()!);
    }

    [Fact]
    public async Task AdminLogin_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = "admin", password = "wrongpassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminLogin_WithUnknownUser_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = "nobody", password = "anything" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── QR login ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task QrLogin_WithUnknownCode_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/qr-login",
            new { qr_code = "this-qr-code-does-not-exist" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Token validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateToken_WithValidAdminToken_ReturnsIsValid()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/validate-token");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(data);
        Assert.Equal("true", data["isValid"].ToString()!.ToLower());
    }

    [Fact]
    public async Task ValidateToken_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/validate-token", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Forced password change enforcement (H-01) ─────────────────────────────

    [Fact]
    public async Task AdminWithPendingPasswordChange_CannotUsePrivilegedEndpoints()
    {
        var token = await GetAdminTokenAsync();
        await SetAdminRequiresPasswordChangeAsync(true);

        try
        {
            // Privileged call is rejected until the password is changed.
            var createScene = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
            {
                Content = JsonContent.Create(new { name = "blocked-scene" })
            };
            createScene.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var blocked = await _client.SendAsync(createScene);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

            // Token validation stays reachable so the frontend can redirect.
            var validate = new HttpRequestMessage(HttpMethod.Post, "/api/validate-token");
            validate.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var validateResponse = await _client.SendAsync(validate);
            Assert.Equal(HttpStatusCode.OK, validateResponse.StatusCode);

            // The change-password endpoint itself stays reachable (wrong current
            // password here — 401 proves it passed the enforcement gate).
            var change = new HttpRequestMessage(HttpMethod.Post, "/api/changeAdminPassword")
            {
                Content = JsonContent.Create(new
                {
                    username = PlsWebAppFactory.TestAdminUsername,
                    password = "wrong-current-password",
                    newpassword = "irrelevant-new-password"
                })
            };
            change.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var changeResponse = await _client.SendAsync(change);
            Assert.Equal(HttpStatusCode.Unauthorized, changeResponse.StatusCode);
        }
        finally
        {
            await SetAdminRequiresPasswordChangeAsync(false);
        }
    }

    [Fact]
    public async Task AdminWithPendingPasswordChange_CanReachChangePasswordEndpoint_WithNonCanonicalCasing()
    {
        var token = await GetAdminTokenAsync();
        await SetAdminRequiresPasswordChangeAsync(true);

        try
        {
            // Route matching is case-insensitive, so this reaches the controller —
            // the enforcement middleware must exempt it the same way, not 403 first.
            var change = new HttpRequestMessage(HttpMethod.Post, "/api/ChangeAdminPassword")
            {
                Content = JsonContent.Create(new
                {
                    username = PlsWebAppFactory.TestAdminUsername,
                    password = "wrong-current-password",
                    newpassword = "irrelevant-new-password"
                })
            };
            change.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var changeResponse = await _client.SendAsync(change);
            Assert.Equal(HttpStatusCode.Unauthorized, changeResponse.StatusCode);
        }
        finally
        {
            await SetAdminRequiresPasswordChangeAsync(false);
        }
    }

    [Fact]
    public async Task AdminWithPendingPasswordChange_CanLogout()
    {
        var token = await GetAdminTokenAsync();
        await SetAdminRequiresPasswordChangeAsync(true);

        try
        {
            var logout = new HttpRequestMessage(HttpMethod.Post, "/api/logout")
            {
                Content = JsonContent.Create(new { refreshToken = "irrelevant" })
            };
            logout.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var logoutResponse = await _client.SendAsync(logout);
            Assert.NotEqual(HttpStatusCode.Forbidden, logoutResponse.StatusCode);
        }
        finally
        {
            await SetAdminRequiresPasswordChangeAsync(false);
        }
    }

    private async Task SetAdminRequiresPasswordChangeAsync(bool value)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
        var admin = await db.Users.SingleAsync(u => u.Username == PlsWebAppFactory.TestAdminUsername);
        admin.RequiresPasswordChange = value;
        await db.SaveChangesAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    internal async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return data!["token"].ToString()!;
    }
}
