using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PLS.Api.Tests;

[Collection("Integration")]
public class QrCodeTests
{
    private readonly HttpClient _client;

    public QrCodeTests(PlsWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Patient QR codes ───────────────────────────────────────────────────────

    [Fact]
    public async Task GeneratePatientQRCodes_ReturnsRequestedCount()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 5 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal("success", data!["status"].GetString()!.ToLower());
        Assert.Equal(5, data["qrcodes"].GetArrayLength());
    }

    [Fact]
    public async Task GeneratePatientQRCodes_AllCodesAreUnique()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 10 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var codes = data!["qrcodes"].EnumerateArray().Select(e => e.GetString()).ToList();

        Assert.Equal(codes.Count, codes.Distinct().Count());
        // Each code should be 64 characters
        Assert.All(codes, c => Assert.Equal(64, c!.Length));
    }

    [Fact]
    public async Task GetUnusedPatientQrCodes_ReturnsOnlyUnlinkedCodes()
    {
        var token = await GetAdminTokenAsync();

        // Generate 3 new codes
        var genReq = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 3 })
        };
        genReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(genReq);

        var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/getUnusedPatientQrCodes");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(getReq);
        response.EnsureSuccessStatusCode();

        var codes = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(codes);
        Assert.True(codes.Count >= 3);
    }

    [Fact]
    public async Task DevUnusedPatientQrCodes_WithAdminToken_Returns404_WhenFeatureDisabled()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dev/unusedPatientQrCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DevUnusedPatientQrCodes_WithUserToken_Returns404_WhenFeatureDisabled()
    {
        var token = await GetUserTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dev/unusedPatientQrCodes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Login QR codes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateLoginQRCodes_ReturnsRequestedCount()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generateLoginQRCodes")
        {
            Content = JsonContent.Create(new { number = 3 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal("success", data!["status"].GetString()!.ToLower());
        Assert.Equal(3, data["qrcodes"].GetArrayLength());
    }

    [Fact]
    public async Task GetLoginQrCodes_ReturnsAllLoginCodes()
    {
        var token = await GetAdminTokenAsync();

        // Generate some login codes first
        var genReq = new HttpRequestMessage(HttpMethod.Post, "/api/generateLoginQRCodes")
        {
            Content = JsonContent.Create(new { number = 2 })
        };
        genReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await _client.SendAsync(genReq);

        var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/getLoginQrCodes");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(getReq);
        response.EnsureSuccessStatusCode();

        var codes = await response.Content.ReadFromJsonAsync<List<string>>();
        Assert.NotNull(codes);
        Assert.True(codes.Count >= 2);
    }

    [Fact]
    public async Task QrLogin_WithGeneratedLoginCode_ReturnsToken()
    {
        var token = await GetAdminTokenAsync();

        // Generate a login QR code
        var genReq = new HttpRequestMessage(HttpMethod.Post, "/api/generateLoginQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        genReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var genResp = await _client.SendAsync(genReq);
        var genData = await genResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var qrCode = genData!["qrcodes"][0].GetString()!;

        // Log in with that QR code
        var loginResponse = await _client.PostAsJsonAsync("/api/qr-login", new { qr_code = qrCode });
        loginResponse.EnsureSuccessStatusCode();

        var loginData = await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("success", loginData!["status"].ToString()!.ToLower());
        Assert.NotEmpty(loginData["token"].ToString()!);
    }

    // ── Login-QR single-use lifecycle (H-02) ──────────────────────────────────

    [Fact]
    public async Task QrLogin_SecondUseOfSameCode_Returns401()
    {
        var qrCode = await GenerateLoginQrCodeAsync();

        var first = await _client.PostAsJsonAsync("/api/qr-login", new { qr_code = qrCode });
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsJsonAsync("/api/qr-login", new { qr_code = qrCode });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task GetLoginQrCodes_ExcludesConsumedCodes()
    {
        var token = await GetAdminTokenAsync();
        var qrCode = await GenerateLoginQrCodeAsync();

        var before = await FetchLoginQrCodesAsync(token);
        Assert.Contains(qrCode, before);

        var login = await _client.PostAsJsonAsync("/api/qr-login", new { qr_code = qrCode });
        login.EnsureSuccessStatusCode();

        var after = await FetchLoginQrCodesAsync(token);
        Assert.DoesNotContain(qrCode, after);
    }

    [Fact]
    public async Task QrLogin_ConcurrentDoubleRedemption_ExactlyOneSucceeds()
    {
        var qrCode = await GenerateLoginQrCodeAsync();

        var attempts = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/qr-login", new { qr_code = qrCode }),
            _client.PostAsJsonAsync("/api/qr-login", new { qr_code = qrCode }));

        Assert.Equal(1, attempts.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(1, attempts.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    private async Task<string> GenerateLoginQrCodeAsync()
    {
        var token = await GetAdminTokenAsync();
        var genReq = new HttpRequestMessage(HttpMethod.Post, "/api/generateLoginQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        genReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var genResp = await _client.SendAsync(genReq);
        genResp.EnsureSuccessStatusCode();
        var genData = await genResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return genData!["qrcodes"][0].GetString()!;
    }

    private async Task<List<string>> FetchLoginQrCodesAsync(string token)
    {
        var getReq = new HttpRequestMessage(HttpMethod.Get, "/api/getLoginQrCodes");
        getReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(getReq);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<string>>())!;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return data!["token"].ToString()!;
    }

    private async Task<string> GetUserTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/user-login",
            new { username = PlsWebAppFactory.TestUserUsername, password = PlsWebAppFactory.TestUserPassword });
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return data!["token"].ToString()!;
    }
}
