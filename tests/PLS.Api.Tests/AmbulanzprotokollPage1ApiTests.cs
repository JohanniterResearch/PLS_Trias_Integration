using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PLS.Api.Tests;

[Collection("Integration")]
public class AmbulanzprotokollPage1ApiTests
{
    private readonly HttpClient _client;

    public AmbulanzprotokollPage1ApiTests(PlsWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task Endpoint_WithNoToken_Returns401(string method)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), "/api/v2/persons/1/ambulanzprotokoll-page1");
        if (method == "PUT")
            request.Content = JsonContent.Create(new { status = "draft", formState = new { } });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenNoRecordExists_ReturnsDefaultDraftState()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal(patientId, payload["patientId"].GetInt32());
        Assert.Equal("draft", payload["status"].GetString());
        Assert.True(payload["formState"].TryGetProperty("incident", out var incident));
        Assert.Equal(string.Empty, incident.GetProperty("ambulanzort").GetString());
        Assert.True(payload["formState"].TryGetProperty("assessment_secondary", out var secondary));
        Assert.Equal(JsonValueKind.Array, secondary.GetProperty("bodymap").ValueKind);
        Assert.Equal(JsonValueKind.Null, payload["finalizedAt"].ValueKind);
    }

    [Fact]
    public async Task Get_WhenPatientDoesNotExist_Returns404()
    {
        var token = await GetAdminTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v2/persons/999999/ambulanzprotokoll-page1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_ThenGet_RoundTripsNestedJson()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var formState = new
        {
            incident = new
            {
                ambulanzort = "Wien",
                datum = "2026-03-26",
                uhrzeit_beginn = "08:15"
            },
            assessment_secondary = new
            {
                anamnese_text = "Testanamnese",
                bodymap = new[]
                {
                    new { view = "front", marker = "schmerz", x = 10, y = 20 }
                }
            },
            disposition = new
            {
                kontaktdaten = "Mustergasse 1"
            }
        };

        await PutFormStateAsync(token, patientId, "draft", formState);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(getRequest);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal("draft", payload["status"].GetString());
        var returnedState = payload["formState"];
        Assert.Equal("Wien", returnedState.GetProperty("incident").GetProperty("ambulanzort").GetString());
        Assert.Equal("08:15", returnedState.GetProperty("incident").GetProperty("uhrzeit_beginn").GetString());
        Assert.Equal("Testanamnese", returnedState.GetProperty("assessment_secondary").GetProperty("anamnese_text").GetString());
        Assert.Equal("schmerz", returnedState.GetProperty("assessment_secondary").GetProperty("bodymap")[0].GetProperty("marker").GetString());
        Assert.Equal("Mustergasse 1", returnedState.GetProperty("disposition").GetProperty("kontaktdaten").GetString());
    }

    [Fact]
    public async Task Forms_AreIsolated_PerPatient()
    {
        var token = await GetAdminTokenAsync();
        var patientA = await CreatePatientAsync(token);
        var patientB = await CreatePatientAsync(token);

        await PutFormStateAsync(token, patientA, "draft", new { incident = new { ambulanzort = "Ort A" } });
        await PutFormStateAsync(token, patientB, "draft", new { incident = new { ambulanzort = "Ort B" } });

        var stateA = await GetFormStateAsync(token, patientA);
        var stateB = await GetFormStateAsync(token, patientB);

        Assert.Equal("Ort A", stateA.GetProperty("incident").GetProperty("ambulanzort").GetString());
        Assert.Equal("Ort B", stateB.GetProperty("incident").GetProperty("ambulanzort").GetString());
    }

    [Fact]
    public async Task Finalize_WithPartialData_Succeeds()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1")
        {
            Content = JsonContent.Create(new
            {
                status = "finalized",
                formState = new
                {
                    incident = new { ambulanzort = "Finalisiert" }
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(payload);
        Assert.Equal("finalized", payload["status"].GetString());
        Assert.Equal("Finalisiert", payload["formState"].GetProperty("incident").GetProperty("ambulanzort").GetString());
        Assert.Equal(JsonValueKind.String, payload["finalizedAt"].ValueKind);
    }

    [Fact]
    public async Task Put_WithInvalidStatus_Returns400()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1")
        {
            Content = JsonContent.Create(new
            {
                status = "archived",
                formState = new { incident = new { ambulanzort = "X" } }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResponseShape_RemainsStable_OnGetAndPut()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var getResponse = await _client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();

        var getPayload = await getResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(getPayload);
        Assert.Equal(
            ["finalizedAt", "formState", "patientId", "revision", "status", "updatedAt"],
            getPayload.Keys.OrderBy(key => key).ToArray());

        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1")
        {
            Content = JsonContent.Create(new
            {
                status = "draft",
                formState = new { incident = new { ambulanzort = "Shape" } }
            })
        };
        putRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var putResponse = await _client.SendAsync(putRequest);
        putResponse.EnsureSuccessStatusCode();

        var putPayload = await putResponse.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(putPayload);
        Assert.Equal(
            ["finalizedAt", "formState", "patientId", "revision", "status", "updatedAt"],
            putPayload.Keys.OrderBy(key => key).ToArray());
    }

    // ── Optimistic concurrency (revision) ──────────────────────────────────────

    [Fact]
    public async Task Put_WithoutExpectedRevision_SucceedsAndIncrementsRevision()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var first = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "A" } }
        });
        first.EnsureSuccessStatusCode();
        var firstPayload = await first.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var firstRevision = firstPayload!["revision"].GetInt32();
        Assert.True(firstRevision >= 1);

        var second = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "B" } }
        });
        second.EnsureSuccessStatusCode();
        var secondPayload = await second.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal(firstRevision + 1, secondPayload!["revision"].GetInt32());
    }

    [Fact]
    public async Task Put_WithStaleExpectedRevision_Returns409WithCurrentRevision()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var first = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "A" } }
        });
        first.EnsureSuccessStatusCode();
        var currentRevision = (await first.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["revision"].GetInt32();

        var stale = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "Conflicting" } },
            expectedRevision = currentRevision - 1
        });

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var conflictPayload = await stale.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal(currentRevision, conflictPayload!["revision"].GetInt32());
    }

    [Fact]
    public async Task Put_WithMatchingExpectedRevision_Succeeds()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var first = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "A" } }
        });
        first.EnsureSuccessStatusCode();
        var currentRevision = (await first.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["revision"].GetInt32();

        var response = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "B" } },
            expectedRevision = currentRevision
        });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Put_ModifyingFinalizedRecord_Returns409()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var finalize = await PutRawAsync(token, patientId, new
        {
            status = "finalized",
            formState = new { incident = new { ambulanzort = "Final" } }
        });
        finalize.EnsureSuccessStatusCode();

        var attemptChange = await PutRawAsync(token, patientId, new
        {
            status = "finalized",
            formState = new { incident = new { ambulanzort = "Changed" } }
        });

        Assert.Equal(HttpStatusCode.Conflict, attemptChange.StatusCode);
    }

    [Fact]
    public async Task Put_ReFinalizingWithIdenticalContent_IsIdempotent()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var formState = new { incident = new { ambulanzort = "Same" } };
        var finalize = await PutRawAsync(token, patientId, new { status = "finalized", formState });
        finalize.EnsureSuccessStatusCode();
        var revision = (await finalize.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["revision"].GetInt32();

        var repeat = await PutRawAsync(token, patientId, new { status = "finalized", formState });
        repeat.EnsureSuccessStatusCode();
        var repeatPayload = await repeat.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.Equal(revision, repeatPayload!["revision"].GetInt32());
    }

    [Fact]
    public async Task Put_UnFinalizingViaDraftStatus_Returns409()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var finalize = await PutRawAsync(token, patientId, new
        {
            status = "finalized",
            formState = new { incident = new { ambulanzort = "Final" } }
        });
        finalize.EnsureSuccessStatusCode();
        var finalized = await finalize.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();

        // A draft-status resubmit against an already-finalized record must not
        // bypass the immutability guard just because it isn't status="finalized".
        var attemptUnFinalize = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "Snuck in" } }
        });

        Assert.Equal(HttpStatusCode.Conflict, attemptUnFinalize.StatusCode);

        var stillFinalized = await GetFormStateAsync(token, patientId);
        Assert.Equal("Final", stillFinalized.GetProperty("incident").GetProperty("ambulanzort").GetString());
    }

    [Fact]
    public async Task Put_ConcurrentUpdatesOnSameRevision_ExactlyOneSucceeds()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);

        var first = await PutRawAsync(token, patientId, new
        {
            status = "draft",
            formState = new { incident = new { ambulanzort = "Base" } }
        });
        first.EnsureSuccessStatusCode();
        var currentRevision = (await first.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["revision"].GetInt32();

        var attempts = await Task.WhenAll(
            PutRawAsync(token, patientId, new
            {
                status = "draft",
                formState = new { incident = new { ambulanzort = "WriterA" } },
                expectedRevision = currentRevision
            }),
            PutRawAsync(token, patientId, new
            {
                status = "draft",
                formState = new { incident = new { ambulanzort = "WriterB" } },
                expectedRevision = currentRevision
            }));

        Assert.Equal(1, attempts.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(1, attempts.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var winnerName = (await attempts.First(r => r.IsSuccessStatusCode).Content
            .ReadFromJsonAsync<Dictionary<string, JsonElement>>())!["formState"]
            .GetProperty("incident").GetProperty("ambulanzort").GetString();

        var stored = await GetFormStateAsync(token, patientId);
        Assert.Equal(winnerName, stored.GetProperty("incident").GetProperty("ambulanzort").GetString());
    }

    private async Task<HttpResponseMessage> PutRawAsync(string token, int patientId, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> GetFormStateAsync(string token, int patientId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return payload!["formState"];
    }

    private async Task PutFormStateAsync(string token, int patientId, string status, object formState)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v2/persons/{patientId}/ambulanzprotokoll-page1")
        {
            Content = JsonContent.Create(new
            {
                status,
                formState
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

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
        return data!["patientId"].GetInt32();
    }

    private async Task<int> CreateSceneAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = $"Ambdoku {Guid.NewGuid():N}" })
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
}
