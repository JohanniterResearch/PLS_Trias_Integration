using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PLS.Api.Models.Entities;

namespace PLS.Api.Tests;

[Collection("Integration")]
public class BodyPartTests
{
    private readonly HttpClient _client;

    public BodyPartTests(PlsWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBodyParts_ReturnsAllBodyPartKeys()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/get-body-parts?idpatient={patientId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var bodyParts = ExtractBodyParts(payload!);
        Assert.NotNull(bodyParts);
        // All body-part keys from BodyPartConstants should be present
        Assert.Contains("kopf_vorne", bodyParts.Keys);
        Assert.Contains("brust_links", bodyParts.Keys);
        Assert.Contains("fuss_rechts_hinten", bodyParts.Keys);
        Assert.Equal(BodyPartConstants.ValidKeys.Count, bodyParts.Count);
        // All default to 0
        Assert.All(bodyParts.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public async Task SaveBodyPart_UpdatesSingleKeyInJsonb()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        // Set kopf_vorne to 1
        var saveRequest = new HttpRequestMessage(HttpMethod.Put, "/api/save-body-part")
        {
            Content = JsonContent.Create(new
            {
                bodyPartId = "kopf_vorne",
                isClicked = 1,
                idpatient = patientId
            })
        };
        saveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var saveResponse = await _client.SendAsync(saveRequest);
        saveResponse.EnsureSuccessStatusCode();

        // Verify the value was persisted
        var getRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/get-body-parts?idpatient={patientId}");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResponse = await _client.SendAsync(getRequest);
        var payload = await getResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var bodyParts = ExtractBodyParts(payload!);

        Assert.Equal(1, bodyParts!["kopf_vorne"]);
        // Other keys stay 0
        Assert.Equal(0, bodyParts["brust_links"]);
    }

    [Fact]
    public async Task SaveBodyPart_WithInvalidKey_Returns400()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/save-body-part")
        {
            Content = JsonContent.Create(new
            {
                bodyPartId = "nonexistent_key",
                isClicked = 1,
                idpatient = patientId
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return data!["token"].ToString()!;
    }

    private async Task<int> CreatePatientAsync(string token)
    {
        // Generate QR code
        var qrReq = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        qrReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var qrResp = await _client.SendAsync(qrReq);
        var qrData = await qrResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var qrCode = qrData!["qrcodes"][0].GetString()!;

        // Create operation scene
        var sceneReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = "Body Test Scene" })
        };
        sceneReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sceneResp = await _client.SendAsync(sceneReq);
        var sceneData = await sceneResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var sceneId = sceneData!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!["idoperationScene"].GetInt32();

        // Scan QR code to create patient
        var scanReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        scanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var scanResp = await _client.SendAsync(scanReq);
        var scanData = await scanResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return scanData!["patientId"].GetInt32();
    }

    private static Dictionary<string, int> ExtractBodyParts(Dictionary<string, JsonElement> payload)
    {
        return payload
            .Where(kvp => kvp.Key is not ("idbody" or "idpatient" or "created_at" or "updated_at"))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.GetInt32());
    }
}
