using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PLS.Api.Tests;

/// <summary>
/// Task 57: Concurrency and Consistency Tests.
/// Validates behaviour under concurrent and rapid sequential writes:
///  - Concurrent triage updates on the same patient complete without error
///  - Final state is always a valid value (no corruption/partial write)
///  - Scanning the same patient QR code concurrently is idempotent (same patient ID returned)
///  - Rapid sequential updates persist each write correctly
/// </summary>
[Collection("Integration")]
public class ConcurrencyTests
{
    private readonly HttpClient _client;

    public ConcurrencyTests(PlsWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Concurrent triage color update ────────────────────────────────────────

    [Fact]
    public async Task ConcurrentTriageColorUpdates_BothSucceed_NoCorruption()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        // Launch two concurrent updates with different triage colors
        var task1 = UpdateTriageColorAsync(token, patientId, "rot");
        var task2 = UpdateTriageColorAsync(token, patientId, "grün");
        var results = await Task.WhenAll(task1, task2);
        var (response1, response2) = (results[0], results[1]);

        // Both requests must complete successfully — no 500s
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Each response reflects the triagefarbe value at the time that request wrote to DB
        var data1 = await response1.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var data2 = await response2.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        var color1 = data1!["triagefarbe"].GetString();
        var color2 = data2!["triagefarbe"].GetString();

        // Each individual response must be a valid color (not null or empty)
        Assert.Contains(color1, new[] { "rot", "grün" });
        Assert.Contains(color2, new[] { "rot", "grün" });
    }

    [Fact]
    public async Task ConcurrentTriageColorUpdates_FinalStateIsValid()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        var validColors = new HashSet<string?> { "rot", "grün", "gelb", "schwarz" };

        // Send concurrent updates with different valid colors
        var tasks = validColors.Select(color => UpdateTriageColorAsync(token, patientId, color!));
        var responses = await Task.WhenAll(tasks);

        // All must succeed
        foreach (var r in responses)
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        // Final state read from DB must be one of the submitted colors
        var getRequest = new HttpRequestMessage(
            HttpMethod.Post, "/api/v2/persons")
        {
            Content = JsonContent.Create(new { operationSceneId = await GetSceneIdForPatientAsync(token, patientId) })
        };
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();

        var patients = await getResponse.Content.ReadFromJsonAsync<List<Dictionary<string, JsonElement>>>();
        var finalColor = patients!.First(p => p["idpatient"].GetInt32() == patientId)["triagefarbe"].GetString();

        Assert.Contains(finalColor, (IEnumerable<string?>)validColors);
    }

    // ── Concurrent QR code scan (idempotency) ─────────────────────────────────

    [Fact]
    public async Task ConcurrentQrCodeScans_SameCode_ReturnSamePatientId()
    {
        var token = await GetAdminTokenAsync();

        // Generate a single patient QR code and a scene
        var qrCode = await GeneratePatientQrCodeAsync(token);
        var sceneId = await CreateSceneAsync(token);

        // Scan the same QR code concurrently from two "devices"
        var scan1 = ScanQrCodeAsync(token, qrCode, sceneId);
        var scan2 = ScanQrCodeAsync(token, qrCode, sceneId);
        var ids = await Task.WhenAll(scan1, scan2);
        var (id1, id2) = (ids[0], ids[1]);

        // Both scans must resolve to the same patient — QR scan must be idempotent
        Assert.Equal(id1, id2);
        Assert.True(id1 > 0);
    }

    // ── Rapid sequential updates ───────────────────────────────────────────────

    [Fact]
    public async Task RapidSequentialTriageUpdates_EachPersistsCorrectly()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        var sequence = new[] { "rot", "grün", "gelb", "schwarz", "grün", "rot" };

        string? lastColor = null;
        foreach (var color in sequence)
        {
            var response = await UpdateTriageColorAsync(token, patientId, color);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
            lastColor = data!["triagefarbe"].GetString();
        }

        // The last update's color must match the final write
        Assert.Equal(sequence[^1], lastColor);
    }

    [Fact]
    public async Task RapidRespirationAndColorUpdates_DoNotInterfere()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        // Alternate between respiration and color updates
        var respTask = Task.Run(async () =>
        {
            for (int i = 0; i < 3; i++)
            {
                var req = new HttpRequestMessage(
                    HttpMethod.Post, $"/api/v2/persons/{patientId}/respiration")
                {
                    Content = JsonContent.Create(new { respiration = i % 2 == 0 })
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var resp = await _client.SendAsync(req);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            }
        });

        var colorTask = Task.Run(async () =>
        {
            var colors = new[] { "rot", "grün", "gelb" };
            foreach (var color in colors)
            {
                var resp = await UpdateTriageColorAsync(token, patientId, color);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            }
        });

        await Task.WhenAll(respTask, colorTask);
        // Test passes if neither task throws — concurrent field updates to the same patient
        // complete without EF exceptions or server errors
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

    private async Task<int> CreatePatientAsync(string token)
    {
        var qrCode = await GeneratePatientQrCodeAsync(token);
        var sceneId = await CreateSceneAsync(token);
        return await ScanQrCodeAsync(token, qrCode, sceneId);
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

    // scene ID stored per-patient for the persons-list query
    private readonly Dictionary<int, int> _patientSceneId = new();

    private async Task<int> CreateSceneAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = $"Concurrency Test {Guid.NewGuid():N}" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!["idoperationScene"].GetInt32();
    }

    private async Task<int> ScanQrCodeAsync(string token, string qrCode, int sceneId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var patientId = data!["patientId"].GetInt32();
        _patientSceneId[patientId] = sceneId;
        return patientId;
    }

    private async Task<int> GetSceneIdForPatientAsync(string token, int patientId)
    {
        // If scene not cached, create a new one (patient is in whatever scene it was created in)
        if (_patientSceneId.TryGetValue(patientId, out var sceneId))
            return sceneId;

        return await CreateSceneAsync(token);
    }

    private async Task<HttpResponseMessage> UpdateTriageColorAsync(string token, int patientId, string color)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new { triageColor = color })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
