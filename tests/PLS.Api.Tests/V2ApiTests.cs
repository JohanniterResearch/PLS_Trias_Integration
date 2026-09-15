using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PLS.Api.Data;
using PLS.Api.Models.Entities;

namespace PLS.Api.Tests;

/// <summary>
/// Task 53: Integration tests for v2 clean API contracts.
/// Validates that:
///  - v2 endpoints accept operationSceneId as a typed integer (not a stringified JSON object)
///  - v2 createOperationScene returns "operationScene" key (correct spelling)
///  - v2 triage update accepts boolean respiration/blutung
/// </summary>
[Collection("Integration")]
public class V2ApiTests
{
    private readonly PlsWebAppFactory _factory;
    private readonly HttpClient _client;

    public V2ApiTests(PlsWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TriagefarbeConstraint_RejectsInvalidDirectDatabaseWrite()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlsDbContext>();
        var patient = new Patient { Triagefarbe = "orange" };

        db.Patients.Add(patient);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    // ── v2 createOperationScene: correct spelling ─────────────────────────────

    [Fact]
    public async Task V2CreateOperationScene_ReturnsOperationSceneKey_NotTypo()
    {
        var token = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = "V2 Test Scene" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(data);

        // v2 must use the correctly-spelled key
        Assert.True(data.ContainsKey("operationScene"),
            "v2 response must contain 'operationScene' (not the v1 typo 'operactionScene')");
        Assert.False(data.ContainsKey("operactionScene"),
            "v2 response must NOT contain the v1 typo 'operactionScene'");

        var scene = data["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!;
        Assert.True(scene["idoperationScene"].GetInt32() > 0);
    }

    [Fact]
    public async Task V2CreateOperationScene_WithId_UpdatesExistingScene()
    {
        var token = await GetAdminTokenAsync();
        var sceneId = await CreateSceneAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { id = sceneId, name = "Updated Name", description = "Neu" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var scene = data!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!;
        Assert.Equal("Updated Name", scene["name"].GetString());
        Assert.Equal(sceneId, scene["idoperationScene"].GetInt32());
    }

    [Fact]
    public async Task V2DeleteOperationScene_RemovesSceneFromList()
    {
        var token = await GetAdminTokenAsync();
        var sceneId = await CreateSceneAsync(token);

        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/deleteOperationScene/{sceneId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        var deleteData = await deleteResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal("success", deleteData!["status"].GetString());

        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v2/getAllCurrentOperationScenes");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResponse = await _client.SendAsync(getRequest);
        var scenes = await getResponse.Content.ReadFromJsonAsync<List<Dictionary<string, JsonElement>>>();
        Assert.DoesNotContain(scenes!, s => s["idoperationScene"].GetInt32() == sceneId);
    }

    // ── v2 GET persons: typed operationSceneId ────────────────────────────────

    [Fact]
    public async Task V2GetPersons_WithTypedSceneId_ReturnsPatients()
    {
        var token = await GetAdminTokenAsync();
        var (patientId, sceneId) = await CreatePatientInSceneAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/persons")
        {
            Content = JsonContent.Create(new { operationSceneId = sceneId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var patients = await response.Content.ReadFromJsonAsync<List<Dictionary<string, JsonElement>>>();
        Assert.NotNull(patients);
        Assert.Contains(patients, p => p["idpatient"].GetInt32() == patientId);
    }

    // ── v2 verify-patient-qr-code: typed operationSceneId ─────────────────────

    [Fact]
    public async Task V2VerifyPatientQrCode_WithTypedSceneId_CreatesPatient()
    {
        var token = await GetAdminTokenAsync();
        var sceneId = await CreateSceneAsync(token);
        var qrCode = await GeneratePatientQrCodeAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(data);
        Assert.True(data["patientId"].GetInt32() > 0);
    }

    [Fact]
    public async Task V2VerifyPatientQrCode_WithMissingScene_ReturnsNotFound()
    {
        var token = await GetAdminTokenAsync();
        var qrCode = await GeneratePatientQrCodeAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = 999_999 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Einsatzort nicht gefunden", data!["message"].GetString());
    }

    [Fact]
    public async Task V2VerifyPatientQrCode_SameCodeTwice_ReturnsSamePatientId()
    {
        var token = await GetAdminTokenAsync();
        var sceneId = await CreateSceneAsync(token);
        var qrCode = await GeneratePatientQrCodeAsync(token);

        async Task<int> Scan()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
            {
                Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resp = await _client.SendAsync(req);
            resp.EnsureSuccessStatusCode();
            var d = await resp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
            return d!["patientId"].GetInt32();
        }

        Assert.Equal(await Scan(), await Scan());
    }

    [Fact]
    public async Task V2VerifyPatientQrCode_ConcurrentScans_ReturnSamePatientId()
    {
        var token = await GetAdminTokenAsync();
        var sceneId = await CreateSceneAsync(token);
        var qrCode = await GeneratePatientQrCodeAsync(token);

        Task<int> ScanAsync()
        {
            return Task.Run(async () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
                {
                    Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await _client.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var data = await resp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
                return data!["patientId"].GetInt32();
            });
        }

        var ids = await Task.WhenAll(ScanAsync(), ScanAsync());

        Assert.Equal(ids[0], ids[1]);
        Assert.True(ids[0] > 0);
    }

    // ── v2 update-triage-color: boolean respiration ───────────────────────────

    [Fact]
    public async Task V2UpdateTriageColor_WithBooleanRespiration_Succeeds()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new
            {
                triageColor = "grün",
                respiration = true,
                blutung = false
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal("grün", data!["triagefarbe"].GetString());

        // C-02 regression: legacy string storage rendered "False" as truthy in the UI.
        // The contract is real JSON booleans end-to-end.
        Assert.Equal(JsonValueKind.True, data["atmung"].ValueKind);
        Assert.Equal(JsonValueKind.False, data["blutung"].ValueKind);
    }

    [Fact]
    public async Task V2UpdateTriageColor_UnknownPatient_Returns404()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(
            HttpMethod.Post, "/api/v2/persons/999999/update-triage-color")
        {
            Content = JsonContent.Create(new { triageColor = "rot" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task V2UpdateTriageColor_WhitespaceAndCase_NormalizesStoredValue()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new { triageColor = "  GRÜN  " })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal("grün", data!["triagefarbe"].GetString());
    }

    [Fact]
    public async Task V2UpdateTriageColor_InvalidColor_Returns400()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new
            {
                triageColor = "orange"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task V2UpdateLocation_WritesCoordinates()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/persons/{patientId}/location")
        {
            Content = JsonContent.Create(new
            {
                lat = 48.2,
                lng = 16.37
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal(patientId, data!["idpatient"].GetInt32());
        Assert.Equal(16.37, data["longitude_patient"].GetDouble(), 6);
        Assert.Equal(48.2, data["latitude_patient"].GetDouble(), 6);
        Assert.True(data.ContainsKey("updated_at"));
    }

    [Fact]
    public async Task V2UpdateLocation_UnknownPatient_Returns404()
    {
        var token = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/persons/999999/location")
        {
            Content = JsonContent.Create(new
            {
                lat = 48.2,
                lng = 16.37
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task V2UpdateTriageColor_DoesNotOverwriteExistingLocation()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        await UpdateLocationAsync(patientId, token, 48.21, 16.36);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new
            {
                triageColor = "gelb",
                respiration = false,
                blutung = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal("gelb", data!["triagefarbe"].GetString());
        Assert.Equal(16.36, data["longitude_patient"].GetDouble(), 6);
        Assert.Equal(48.21, data["latitude_patient"].GetDouble(), 6);
    }

    [Fact]
    public async Task V2UpdateRespiration_DoesNotOverwriteExistingLocation()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        await UpdateLocationAsync(patientId, token, 48.205, 16.375);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/persons/{patientId}/respiration")
        {
            Content = JsonContent.Create(new
            {
                respiration = true
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.True(data!["atmung"].GetBoolean());
        Assert.Equal(16.375, data["longitude_patient"].GetDouble(), 6);
        Assert.Equal(48.205, data["latitude_patient"].GetDouble(), 6);
    }

    // ── v2 getAllCurrentOperationScenes ───────────────────────────────────────

    [Fact]
    public async Task V2GetAllCurrentOperationScenes_ReturnsSceneList()
    {
        var token = await GetAdminTokenAsync();
        await CreateSceneAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/getAllCurrentOperationScenes");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var scenes = await response.Content.ReadFromJsonAsync<List<Dictionary<string, JsonElement>>>();
        Assert.NotNull(scenes);
        Assert.NotEmpty(scenes);
        Assert.True(scenes[0].ContainsKey("idoperationScene"));
    }

    // ── v2 endpoints require authorization ────────────────────────────────────

    [Theory]
    [InlineData("POST", "/api/v2/persons")]
    [InlineData("POST", "/api/v2/verify-patient-qr-code")]
    [InlineData("POST", "/api/v2/createOperationScene")]
    [InlineData("POST", "/api/v2/persons/1/location")]
    [InlineData("GET", "/api/v2/getAllCurrentOperationScenes")]
    public async Task V2Endpoint_WithNoToken_Returns401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["token"].GetString()!;
    }

    private async Task<int> CreateSceneAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = $"V2 Test {Guid.NewGuid():N}" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!["idoperationScene"].GetInt32();
    }

    private async Task<string> GeneratePatientQrCodeAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["qrcodes"][0].GetString()!;
    }

    private async Task<int> CreatePatientAsync(string token)
    {
        var (patientId, _) = await CreatePatientInSceneAsync(token);
        return patientId;
    }

    private async Task<(int patientId, int sceneId)> CreatePatientInSceneAsync(string token)
    {
        var sceneId = await CreateSceneAsync(token);
        var qrCode = await GeneratePatientQrCodeAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return (data!["patientId"].GetInt32(), sceneId);
    }

    private async Task UpdateLocationAsync(int patientId, string token, double lat, double lng)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v2/persons/{patientId}/location")
        {
            Content = JsonContent.Create(new { lat, lng })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
}
