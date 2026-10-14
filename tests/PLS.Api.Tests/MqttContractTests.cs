using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace PLS.Api.Tests;

/// <summary>
/// Task 41: MQTT Contract Tests.
/// Validates topic namespace, QoS/retain policy, payload schema, and HTTP resilience
/// when the MQTT broker is unavailable.
///
/// Contract under test (MqttPublishService):
///   - Patient topic:  pls/v1/patients/{id}      QoS=AtLeastOnce  Retain=true
///   - Scene topic:    pls/v1/scenes/{id}/patients  QoS=AtLeastOnce  Retain=true
///   - Payload always includes: schemaVersion (int=1), eventTimestamp, patientId
/// </summary>
[Collection("Integration")]
public class MqttContractTests : IAsyncLifetime
{
    private MqttServer? _broker;
    private int _brokerPort;

    // Captured publishes: (topic, payloadJson, qos, retain)
    private readonly List<CapturedMessage> _messages = [];
    private readonly SemaphoreSlim _messageArrived = new(0, 100);

    private MqttEnabledFactory? _factory;
    private HttpClient? _client;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    public async Task InitializeAsync()
    {
        _brokerPort = FindFreePort();

        var mqttFactory = new MqttFactory();
        _broker = mqttFactory.CreateMqttServer(
            new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointPort(_brokerPort)
                .Build());

        _broker.InterceptingPublishAsync += e =>
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _messages.Add(new CapturedMessage(
                e.ApplicationMessage.Topic,
                payload,
                e.ApplicationMessage.QualityOfServiceLevel,
                e.ApplicationMessage.Retain));
            _messageArrived.Release();
            return Task.CompletedTask;
        };

        await _broker.StartAsync();

        _factory = new MqttEnabledFactory(_brokerPort);
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        if (_factory != null) await _factory.DisposeAsync();
        if (_broker != null) await _broker.StopAsync();
        _messageArrived.Dispose();
    }

    // ── Contract: topic namespace ──────────────────────────────────────────────

    [Fact]
    public async Task TriageColorUpdate_PublishesToVersionedPatientTopic()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        _messages.Clear();

        await UpdateTriageColorAsync(token, patientId, "rot");

        // Wait for the patient message (and possibly the scene message)
        var published = await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(published, "MQTT message was not received within timeout");

        var patientMsg = _messages.FirstOrDefault(m =>
            m.Topic == $"pls/v1/patients/{patientId}");
        Assert.NotNull(patientMsg);
    }

    [Fact]
    public async Task TriageColorUpdate_PublishesSceneTopicWhenPatientIsInScene()
    {
        var token = await GetAdminTokenAsync();
        var (patientId, sceneId) = await CreatePatientInSceneAsync(token);
        _messages.Clear();

        await UpdateTriageColorAsync(token, patientId, "gelb");

        // Wait for both patient and scene messages
        await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5)); // first
        await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5)); // second

        var sceneMsg = _messages.FirstOrDefault(m =>
            m.Topic == $"pls/v1/scenes/{sceneId}/patients");
        Assert.NotNull(sceneMsg);
    }

    // ── Contract: QoS and retain flag ─────────────────────────────────────────

    [Fact]
    public async Task PatientMessage_HasAtLeastOnceQosAndRetainFlag()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        _messages.Clear();

        await UpdateTriageColorAsync(token, patientId, "grün");

        var published = await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(published, "MQTT message was not received within timeout");

        var patientMsg = _messages.First(m => m.Topic.StartsWith("pls/v1/patients/"));
        Assert.Equal(MqttQualityOfServiceLevel.AtLeastOnce, patientMsg.Qos);
        Assert.True(patientMsg.Retain, "Patient MQTT message must have retain=true");
    }

    // ── Contract: payload schema ───────────────────────────────────────────────

    [Fact]
    public async Task PatientMessage_PayloadContainsRequiredSchemaFields()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        _messages.Clear();

        await UpdateTriageColorAsync(token, patientId, "schwarz");

        var published = await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(published, "MQTT message was not received within timeout");

        var patientMsg = _messages.First(m => m.Topic == $"pls/v1/patients/{patientId}");
        var doc = JsonDocument.Parse(patientMsg.PayloadJson);
        var root = doc.RootElement;

        // Required schema fields
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.TryGetProperty("eventTimestamp", out _), "eventTimestamp missing");
        Assert.Equal(patientId, root.GetProperty("patientId").GetInt32());
        Assert.True(root.TryGetProperty("triagefarbe", out _), "triagefarbe missing");
        Assert.True(root.TryGetProperty("atmung", out _), "atmung missing");
        Assert.True(root.TryGetProperty("bodyParts", out _), "bodyParts missing");
    }

    [Fact]
    public async Task PatientPayload_DefaultConfig_IncludesLocationButNotName()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        _messages.Clear();

        await UpdateLocationAsync(_client!, token, patientId, lat: 48.2, lng: 16.37);

        var root = await WaitForPatientPayloadAsync(patientId);
        Assert.Equal(16.37, root.GetProperty("longitude").GetDouble(), 6);
        Assert.Equal(48.2, root.GetProperty("latitude").GetDouble(), 6);
        Assert.False(root.TryGetProperty("name", out _), "name must be redacted by default");
    }

    [Theory]
    [InlineData("Mqtt:RedactLocation")]
    [InlineData("Mqtt:RedactPersonalData")] // legacy switch must still hide location
    public async Task PatientPayload_LocationRedactionEnabled_OmitsLocationAndName(string redactionSetting)
    {
        var redactingFactory = new MqttEnabledFactory(_brokerPort, new()
        {
            [redactionSetting] = "true",
            // Distinct client id so the broker does not disconnect the fixture's publisher.
            ["Mqtt:ClientId"] = "pls-backend-redacting"
        });
        await redactingFactory.InitializeAsync();
        var redactingClient = redactingFactory.CreateClient();
        try
        {
            var token = await GetAdminTokenFromAsync(redactingClient);
            var patientId = await CreatePatientViaAsync(redactingClient, token);
            _messages.Clear();

            await UpdateLocationAsync(redactingClient, token, patientId, lat: 48.2, lng: 16.37);

            var root = await WaitForPatientPayloadAsync(patientId);
            Assert.False(root.TryGetProperty("longitude", out _), "longitude must be redacted");
            Assert.False(root.TryGetProperty("latitude", out _), "latitude must be redacted");
            Assert.False(root.TryGetProperty("name", out _), "name must be redacted");
            Assert.Equal(patientId, root.GetProperty("patientId").GetInt32());
        }
        finally
        {
            // Same teardown as the fixture: the WebApplicationFactory sync Dispose disposes
            // singletons before StopAsync, which breaks MqttPublishService shutdown.
            redactingClient.Dispose();
            await redactingFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task PatientPayload_TriagefarbeReflectsUpdatedValue()
    {
        var token = await GetAdminTokenAsync();
        var patientId = await CreatePatientAsync(token);
        _messages.Clear();

        await UpdateTriageColorAsync(token, patientId, "rot");

        var published = await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(published, "MQTT message was not received within timeout");

        var patientMsg = _messages.First(m => m.Topic == $"pls/v1/patients/{patientId}");
        var doc = JsonDocument.Parse(patientMsg.PayloadJson);
        Assert.Equal("rot", doc.RootElement.GetProperty("triagefarbe").GetString());
    }

    [Fact]
    public async Task SceneMessage_PayloadContainsSchemaVersionAndSceneId()
    {
        var token = await GetAdminTokenAsync();
        var (patientId, sceneId) = await CreatePatientInSceneAsync(token);
        _messages.Clear();

        await UpdateTriageColorAsync(token, patientId, "gelb");

        // Wait for both messages
        await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5));
        await _messageArrived.WaitAsync(TimeSpan.FromSeconds(5));

        var sceneMsg = _messages.FirstOrDefault(m =>
            m.Topic == $"pls/v1/scenes/{sceneId}/patients");
        Assert.NotNull(sceneMsg);

        var doc = JsonDocument.Parse(sceneMsg.PayloadJson);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.True(root.TryGetProperty("eventTimestamp", out _), "eventTimestamp missing");
        Assert.Equal(sceneId, root.GetProperty("sceneId").GetInt32());
        Assert.True(root.TryGetProperty("patientIds", out _), "patientIds missing");
    }

    // ── Resilience: HTTP writes succeed when MQTT is unavailable ──────────────

    [Fact]
    public async Task TriageColorUpdate_SucceedsWhenMqttIsDisabled()
    {
        // The standard PlsWebAppFactory has Mqtt:Enabled=false.
        // This test explicitly verifies that HTTP triage operations succeed
        // even when no MQTT broker is reachable.
        using var disabledFactory = new PlsWebAppFactory();
        await disabledFactory.InitializeAsync();
        using var disabledClient = disabledFactory.CreateClient();

        var token = await GetAdminTokenFromAsync(disabledClient);
        var patientId = await CreatePatientViaAsync(disabledClient, token);

        var request = new HttpRequestMessage(
            HttpMethod.Post, $"api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new { triageColor = "rot" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await disabledClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<string> GetAdminTokenAsync()
        => await GetAdminTokenFromAsync(_client!);

    private static async Task<string> GetAdminTokenFromAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/adminLogin",
            new { username = PlsWebAppFactory.TestAdminUsername, password = PlsWebAppFactory.TestAdminPassword });
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return data!["token"].GetString()!;
    }

    private async Task<int> CreatePatientAsync(string token)
    {
        var (patientId, _) = await CreatePatientInSceneAsync(token);
        return patientId;
    }

    private async Task<(int patientId, int sceneId)> CreatePatientInSceneAsync(string token)
    {
        // Generate patient QR code
        var qrReq = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        qrReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var qrResp = await _client!.SendAsync(qrReq);
        qrResp.EnsureSuccessStatusCode();
        var qrData = await qrResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var qrCode = qrData!["qrcodes"][0].GetString()!;

        // Create operation scene
        var sceneReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = "MQTT Test Scene" })
        };
        sceneReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sceneResp = await _client!.SendAsync(sceneReq);
        sceneResp.EnsureSuccessStatusCode();
        var sceneData = await sceneResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var sceneId = sceneData!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!["idoperationScene"].GetInt32();

        // Scan patient QR code → creates patient
        var scanReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        scanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var scanResp = await _client!.SendAsync(scanReq);
        scanResp.EnsureSuccessStatusCode();
        var scanData = await scanResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return (scanData!["patientId"].GetInt32(), sceneId);
    }

    private async Task<int> CreatePatientViaAsync(HttpClient client, string token)
    {
        var qrReq = new HttpRequestMessage(HttpMethod.Post, "/api/generatePatientQRCodes")
        {
            Content = JsonContent.Create(new { number = 1 })
        };
        qrReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var qrResp = await client.SendAsync(qrReq);
        var qrData = await qrResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var qrCode = qrData!["qrcodes"][0].GetString()!;

        var sceneReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/createOperationScene")
        {
            Content = JsonContent.Create(new { name = "Resilience Test Scene" })
        };
        sceneReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var sceneResp = await client.SendAsync(sceneReq);
        var sceneData = await sceneResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var sceneId = sceneData!["operationScene"].Deserialize<Dictionary<string, JsonElement>>()!["idoperationScene"].GetInt32();

        var scanReq = new HttpRequestMessage(HttpMethod.Post, "/api/v2/verify-patient-qr-code")
        {
            Content = JsonContent.Create(new { qr_code = qrCode, operationSceneId = sceneId })
        };
        scanReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var scanResp = await client.SendAsync(scanReq);
        var scanData = await scanResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return scanData!["patientId"].GetInt32();
    }

    private async Task UpdateTriageColorAsync(string token, int patientId, string color)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"api/v2/persons/{patientId}/update-triage-color")
        {
            Content = JsonContent.Create(new { triageColor = color })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client!.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task UpdateLocationAsync(HttpClient client, string token, int patientId, double lat, double lng)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"api/v2/persons/{patientId}/location")
        {
            Content = JsonContent.Create(new { lat, lng })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<JsonElement> WaitForPatientPayloadAsync(int patientId)
    {
        var topic = $"pls/v1/patients/{patientId}";
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!_messages.Any(m => m.Topic == topic))
        {
            var remaining = deadline - DateTime.UtcNow;
            Assert.True(remaining > TimeSpan.Zero && await _messageArrived.WaitAsync(remaining),
                $"MQTT message on {topic} was not received within timeout");
        }
        return JsonDocument.Parse(_messages.Last(m => m.Topic == topic).PayloadJson).RootElement;
    }

    private static int FindFreePort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    // ── Inner types ────────────────────────────────────────────────────────────

    private record CapturedMessage(
        string Topic,
        string PayloadJson,
        MqttQualityOfServiceLevel Qos,
        bool Retain);

    /// <summary>
    /// Factory variant with MQTT enabled, pointing to a specified local broker port.
    /// All other settings (DB, JWT, credentials) are inherited from PlsWebAppFactory.
    /// </summary>
    private class MqttEnabledFactory : PlsWebAppFactory
    {
        private readonly int _port;
        private readonly Dictionary<string, string> _extraSettings;

        public MqttEnabledFactory(int port, Dictionary<string, string>? extraSettings = null)
        {
            _port = port;
            _extraSettings = extraSettings ?? [];
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Override the disabled-MQTT setting from the base factory
            builder.UseSetting("Mqtt:Enabled", "true");
            builder.UseSetting("Mqtt:BrokerHost", "localhost");
            builder.UseSetting("Mqtt:BrokerPort", _port.ToString());
            foreach (var (key, value) in _extraSettings)
                builder.UseSetting(key, value);
        }
    }
}
