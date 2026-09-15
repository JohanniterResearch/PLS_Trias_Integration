using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PLS.Api.Services;

namespace PLS.Api.Tests;

/// <summary>
/// Task 56: Authorization and Security Test Suite.
/// Validates policy enforcement (AdminOnly, TriageWrite) across protected endpoints:
///  - Negative-path assertions: QR/user tokens denied on AdminOnly endpoints (403)
///  - Unauthenticated denial (401) on all protected endpoints
///  - Positive-path assertions: admin tokens pass AdminOnly, all three token types pass TriageWrite
///  - Refresh token flow: valid token rotates and returns new pair
///  - Revocation: after logout the refresh token is rejected
/// </summary>
[Collection("Integration")]
public class AuthorizationTests
{
    private readonly PlsWebAppFactory _factory;
    private readonly HttpClient _client;

    // Cached within the test-class instance (xUnit runs tests in a class sequentially).
    private string? _cachedAdminToken;
    private string? _cachedQrToken;
    private string? _cachedUserToken;

    public AuthorizationTests(PlsWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── AdminOnly: unauthenticated → 401 ──────────────────────────────────────

    [Theory]
    [InlineData("GET", "api/v2/getAllCurrentOperationScenes")]
    [InlineData("GET", "api/getLoginQrCodes")]
    [InlineData("GET", "api/getUnusedPatientQrCodes")]
    public async Task AdminOnlyEndpoint_WithNoToken_Returns401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── AdminOnly: QR token → 403 ─────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "api/getLoginQrCodes")]
    [InlineData("GET", "api/getUnusedPatientQrCodes")]
    public async Task AdminOnlyEndpoint_WithQrToken_Returns403(string method, string url)
    {
        var token = await GetQrTokenAsync();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AdminOnly: user token → 403 ───────────────────────────────────────────
    // User tokens carry type="qr" by design (testing accommodation, see Decisions table).
    // They therefore cannot satisfy the AdminOnly policy.

    [Theory]
    [InlineData("GET", "api/getLoginQrCodes")]
    [InlineData("GET", "api/getUnusedPatientQrCodes")]
    public async Task AdminOnlyEndpoint_WithUserToken_Returns403(string method, string url)
    {
        var token = await GetUserTokenAsync();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── AdminOnly: admin token → passes authorization ──────────────────────────

    [Theory]
    [InlineData("GET", "api/v2/getAllCurrentOperationScenes")]
    [InlineData("GET", "api/getLoginQrCodes")]
    [InlineData("GET", "api/getUnusedPatientQrCodes")]
    public async Task AdminOnlyEndpoint_WithAdminToken_IsNotDenied(string method, string url)
    {
        var token = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── TriageWrite: unauthenticated → 401 ────────────────────────────────────

    [Fact]
    public async Task TriageWriteEndpoint_WithNoToken_Returns401_GetBodyParts()
    {
        var response = await _client.GetAsync("api/get-body-parts?idpatient=1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TriageWriteEndpoint_WithNoToken_Returns401_PostPersons()
    {
        var response = await _client.PostAsJsonAsync("api/v2/persons", new { operationSceneId = 0 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── QrOrAdmin: unauthenticated → 401 ─────────────────────────────────────

    [Fact]
    public async Task QrOrAdminEndpoint_WithNoToken_Returns401_DevUnusedPatientQrCodes()
    {
        var response = await _client.GetAsync("api/dev/unusedPatientQrCodes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── QrOrAdmin: qr/user/admin tokens are authorized (may return 404) ─────

    [Fact]
    public async Task QrOrAdminEndpoint_WithQrToken_IsNotDenied_DevUnusedPatientQrCodes()
    {
        var token = await GetQrTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "api/dev/unusedPatientQrCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QrOrAdminEndpoint_WithUserToken_IsNotDenied_DevUnusedPatientQrCodes()
    {
        var token = await GetUserTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "api/dev/unusedPatientQrCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task QrOrAdminEndpoint_WithAdminToken_IsNotDenied_DevUnusedPatientQrCodes()
    {
        var token = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "api/dev/unusedPatientQrCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── TriageWrite: QR token → authorized (business logic may return 4xx) ────

    [Fact]
    public async Task TriageWriteEndpoint_WithSceneScopedQrToken_IsNotDenied()
    {
        var (sceneId, patientId) = await CreateSceneWithPatientAsync();
        var token = await SelectSceneAsync(await GetQrTokenAsync(), sceneId);
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/get-body-parts?idpatient={patientId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── TriageWrite: user token → authorized ──────────────────────────────────

    [Fact]
    public async Task TriageWriteEndpoint_WithSceneScopedUserToken_IsNotDenied()
    {
        var (sceneId, patientId) = await CreateSceneWithPatientAsync();
        var token = await SelectSceneAsync(await GetUserTokenAsync(), sceneId);
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/get-body-parts?idpatient={patientId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Scene scoping: cross-scene access is denied (C-05) ────────────────────

    [Fact]
    public async Task TriageWriteEndpoints_WithoutSceneClaim_AreForbiddenForUserToken()
    {
        var (sceneId, patientId) = await CreateSceneWithPatientAsync();
        var token = await GetUserTokenAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "api/v2/persons",
            new { operationSceneId = sceneId }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"api/v2/persons/{patientId}/update-triage-color",
            new { triageColor = "rot" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, $"api/get-body-parts?idpatient={patientId}",
            null, token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Get, $"api/v2/persons/{patientId}/ambulanzprotokoll-page1",
            null, token)).StatusCode);
    }

    [Fact]
    public async Task TriageWriteEndpoints_WithForeignSceneClaim_AreForbidden()
    {
        var (sceneA, patientInA) = await CreateSceneWithPatientAsync();
        var (sceneB, _) = await CreateSceneWithPatientAsync();
        var tokenForB = await SelectSceneAsync(await GetUserTokenAsync(), sceneB);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "api/v2/persons",
            new { operationSceneId = sceneA }, tokenForB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"api/v2/persons/{patientInA}/update-triage-color",
            new { triageColor = "rot" }, tokenForB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"api/v2/persons/{patientInA}/respiration",
            new { respiration = true }, tokenForB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, $"api/v2/persons/{patientInA}/location",
            new { lat = 48.2, lng = 16.4 }, tokenForB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Put, "api/save-body-part",
            new { bodyPartId = "kopf_vorne", isClicked = 1, idpatient = patientInA }, tokenForB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Put, $"api/v2/persons/{patientInA}/ambulanzprotokoll-page1",
            new { status = "draft", formState = new { } }, tokenForB)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(HttpMethod.Post, "api/v2/verify-patient-qr-code",
            new { qr_code = "irrelevant", operationSceneId = sceneA }, tokenForB)).StatusCode);
    }

    [Fact]
    public async Task TriageWriteEndpoints_WithMatchingSceneClaim_Succeed()
    {
        var (sceneId, patientId) = await CreateSceneWithPatientAsync();
        var token = await SelectSceneAsync(await GetUserTokenAsync(), sceneId);

        var persons = await SendAsync(HttpMethod.Post, "api/v2/persons", new { operationSceneId = sceneId }, token);
        persons.EnsureSuccessStatusCode();

        var triage = await SendAsync(HttpMethod.Post, $"api/v2/persons/{patientId}/update-triage-color",
            new { triageColor = "rot" }, token);
        triage.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body, string token)
    {
        var request = new HttpRequestMessage(method, url);
        if (body != null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<string> SelectSceneAsync(string token, int sceneId)
    {
        var response = await SendAsync(HttpMethod.Post, "api/v2/select-scene", new { operationSceneId = sceneId }, token);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["token"].GetString()!;
    }

    private async Task<(int sceneId, int patientId)> CreateSceneWithPatientAsync()
    {
        var adminToken = await GetAdminTokenAsync();

        var sceneResponse = await SendAsync(HttpMethod.Post, "api/v2/createOperationScene",
            new { name = $"AuthZ Scene {Guid.NewGuid():N}" }, adminToken);
        sceneResponse.EnsureSuccessStatusCode();
        var sceneData = await sceneResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var sceneId = sceneData!["operationScene"].GetProperty("idoperationScene").GetInt32();

        var qrResponse = await SendAsync(HttpMethod.Post, "api/generatePatientQRCodes", new { number = 1 }, adminToken);
        qrResponse.EnsureSuccessStatusCode();
        var qrData = await qrResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var qrCode = qrData!["qrcodes"][0].GetString()!;

        var verifyResponse = await SendAsync(HttpMethod.Post, "api/v2/verify-patient-qr-code",
            new { qr_code = qrCode, operationSceneId = sceneId }, adminToken);
        verifyResponse.EnsureSuccessStatusCode();
        var verifyData = await verifyResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return (sceneId, verifyData!["patientId"].GetInt32());
    }

    // ── TriageWrite: admin token → authorized ─────────────────────────────────

    [Fact]
    public async Task TriageWriteEndpoint_WithAdminToken_IsNotDenied()
    {
        var token = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "api/get-body-parts?idpatient=99999");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Refresh token: valid token → new access + refresh tokens ──────────────

    [Fact]
    public async Task RefreshToken_WithValidAdminRefreshToken_ReturnsNewTokens()
    {
        var (_, refreshToken) = await FreshAdminLoginAsync();

        var response = await _client.PostAsJsonAsync("api/refresh-token",
            new { refreshToken });

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(data);
        Assert.Equal("success", data["status"].GetString());
        Assert.True(data.ContainsKey("token"));
        Assert.True(data.ContainsKey("refreshToken"));
        Assert.NotEmpty(data["token"].GetString()!);
        Assert.NotEmpty(data["refreshToken"].GetString()!);
    }

    [Fact]
    public async Task RefreshToken_WithValidUserRefreshToken_ReturnsNewTokens()
    {
        var (_, refreshToken) = await FreshUserLoginAsync();

        var response = await _client.PostAsJsonAsync("api/refresh-token",
            new { refreshToken });

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(data);
        Assert.Equal("success", data["status"].GetString());
        Assert.NotEmpty(data["token"].GetString()!);
    }

    // ── Refresh token: bogus token → 401 ─────────────────────────────────────

    [Fact]
    public async Task RefreshToken_WithInvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("api/refresh-token",
            new { refreshToken = "this-is-not-a-valid-refresh-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Revocation: logout invalidates the refresh token ─────────────────────

    [Fact]
    public async Task Logout_ThenRefreshToken_Returns401()
    {
        var (accessToken, refreshToken) = await FreshAdminLoginAsync();

        // Logout using the access token, passing the refresh token to revoke
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "api/logout")
        {
            Content = JsonContent.Create(new { refreshToken })
        };
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        logoutResponse.EnsureSuccessStatusCode();

        // Revoked refresh token must now be rejected
        var refreshResponse = await _client.PostAsJsonAsync("api/refresh-token",
            new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    // ── Refresh token rotation: used token cannot be replayed ─────────────────

    [Fact]
    public async Task RefreshToken_AfterRotation_OldTokenIsRejected()
    {
        var (_, originalRefreshToken) = await FreshAdminLoginAsync();

        // First refresh — rotates the token
        var firstRefresh = await _client.PostAsJsonAsync("api/refresh-token",
            new { refreshToken = originalRefreshToken });
        firstRefresh.EnsureSuccessStatusCode();

        // Replaying the original refresh token must fail
        var replayResponse = await _client.PostAsJsonAsync("api/refresh-token",
            new { refreshToken = originalRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_ConcurrentDoubleRedemption_ExactlyOneSucceeds()
    {
        var (_, refreshToken) = await FreshAdminLoginAsync();

        var attempts = await Task.WhenAll(
            _client.PostAsJsonAsync("api/refresh-token", new { refreshToken }),
            _client.PostAsJsonAsync("api/refresh-token", new { refreshToken }));

        Assert.Equal(1, attempts.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(1, attempts.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task RefreshTokens_AreStoredHashed_NotPlaintext()
    {
        var (_, refreshToken) = await FreshAdminLoginAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PLS.Api.Data.PlsDbContext>();

        Assert.False(await db.RefreshTokens.AnyAsync(r => r.Token == refreshToken));
        // SHA-256 hex digests only: 64 hex chars per row.
        var tokens = await db.RefreshTokens.Select(r => r.Token).ToListAsync();
        Assert.All(tokens, t => Assert.Matches("^[0-9A-F]{64}$", t));
    }

    // H-03: scene selection must not extend a token's life beyond what it already had —
    // otherwise it becomes an unbounded session-renewal primitive.
    [Fact]
    public async Task MintSceneScopedToken_DoesNotExtendExpiryBeyondOriginalToken()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        var originalExpiry = DateTime.UtcNow.AddMinutes(2); // far shorter than QrExpirationMinutes (480)
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "1"),
            new Claim("type", "qr"),
            new Claim(JwtRegisteredClaimNames.Exp, new DateTimeOffset(originalExpiry).ToUnixTimeSeconds().ToString())
        }));

        var reissued = authService.MintSceneScopedToken(principal, sceneId: 1);
        var reissuedExpiry = new JwtSecurityTokenHandler().ReadJwtToken(reissued).ValidTo;

        Assert.True(Math.Abs((reissuedExpiry - originalExpiry).TotalSeconds) < 5,
            $"expected reissued token to expire at ~{originalExpiry:o}, got {reissuedExpiry:o}");
    }

    private async Task<string> GetAdminTokenAsync()
    {
        if (_cachedAdminToken != null) return _cachedAdminToken;
        var (token, _) = await FreshAdminLoginAsync();
        _cachedAdminToken = token;
        return token;
    }

    private async Task<string> GetUserTokenAsync()
    {
        if (_cachedUserToken != null) return _cachedUserToken;
        var (token, _) = await FreshUserLoginAsync();
        _cachedUserToken = token;
        return token;
    }

    private async Task<string> GetQrTokenAsync()
    {
        if (_cachedQrToken != null) return _cachedQrToken;

        // Generate a fresh login QR code via the admin endpoint
        var adminToken = await GetAdminTokenAsync();
        var genRequest = new HttpRequestMessage(HttpMethod.Post, "api/generateLoginQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        genRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var genResponse = await _client.SendAsync(genRequest);
        genResponse.EnsureSuccessStatusCode();

        var genData = await genResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var qrCode = genData!["qrcodes"][0].GetString()!;

        // Authenticate with the generated QR code
        var loginResponse = await _client.PostAsJsonAsync("api/qr-login", new { qr_code = qrCode });
        loginResponse.EnsureSuccessStatusCode();
        var loginData = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        _cachedQrToken = loginData!["token"].GetString()!;
        return _cachedQrToken;
    }

    /// <summary>Returns a fresh access+refresh token pair for the test admin (not cached).</summary>
    private async Task<(string accessToken, string refreshToken)> FreshAdminLoginAsync()
    {
        var response = await _client.PostAsJsonAsync("api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return (data!["token"].GetString()!, data["refreshToken"].GetString()!);
    }

    /// <summary>Returns a fresh access+refresh token pair for the test user (not cached).</summary>
    private async Task<(string accessToken, string refreshToken)> FreshUserLoginAsync()
    {
        var response = await _client.PostAsJsonAsync("api/user-login",
            new { username = PlsWebAppFactory.TestUserUsername, password = PlsWebAppFactory.TestUserPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return (data!["token"].GetString()!, data["refreshToken"].GetString()!);
    }
}
