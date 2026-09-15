using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using PLS.Api.Data;

namespace PLS.Api.Services;

public class MqttSettings
{
    public bool Enabled { get; set; } = true;
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string ClientId { get; set; } = "pls-backend";
    // Credentials — required in production (allow_anonymous false).
    // Set via Mqtt__Username and Mqtt__Password env vars.
    public string? Username { get; set; }
    public string? Password { get; set; }
    // Reliability: bounded queue for publish operations during broker outages.
    // When full, the oldest pending operation is dropped.
    public int PendingQueueCapacity { get; set; } = 200;
    // Privacy: when true, name/longitude/latitude are omitted from published payloads.
    // Enable for external integrations where location and patient name must not be exposed.
    // Core triage fields (triagefarbe, atmung, blutung, radialispuls, etc.) are always included.
    public bool RedactPersonalData { get; set; } = true;
}

/// <summary>
/// Publishes patient state changes to MQTT. During broker outages, pending publishes
/// are queued (bounded). The queue is drained automatically when the broker reconnects.
/// When the queue is full, the oldest entry is dropped and a warning is emitted.
/// </summary>
public class MqttPublishService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MqttSettings _settings;
    private readonly ILogger<MqttPublishService> _logger;
    private IMqttClient? _client;

    // Pending patient IDs to (re)publish when the broker becomes available.
    // Bounded — protects against unbounded memory growth during extended outages.
    private Channel<int>? _pendingQueue;
    private CancellationTokenSource? _drainCts;

    // Exposed for the health check.
    public bool IsConnected => _client?.IsConnected ?? false;
    public int PendingCount => _pendingQueue?.Reader.Count ?? 0;

    public MqttPublishService(
        IServiceScopeFactory scopeFactory,
        IOptions<MqttSettings> settings,
        ILogger<MqttPublishService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("MQTT publishing is disabled");
            return;
        }

        _pendingQueue = Channel.CreateBounded<int>(new BoundedChannelOptions(_settings.PendingQueueCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

        _drainCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.ConnectedAsync += async _ =>
        {
            _logger.LogInformation("MQTT connected to {Host}:{Port}", _settings.BrokerHost, _settings.BrokerPort);
            await DrainQueueAsync(_drainCts.Token);
        };

        _client.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT disconnected (reason: {Reason}). Reconnecting...", e.ReasonString);
            await ReconnectWithBackoffAsync(cancellationToken);
        };

        try
        {
            await ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MQTT initial connection failed — queuing publishes until reconnected");
        }
    }

    private async Task ReconnectWithBackoffAsync(CancellationToken cancellationToken)
    {
        var delay = TimeSpan.FromSeconds(5);
        const int maxDelaySeconds = 60;

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            try
            {
                await ConnectAsync(cancellationToken);
                return; // ConnectedAsync event will drain the queue
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MQTT reconnection attempt failed. Retrying in {Delay}s", delay.TotalSeconds);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelaySeconds));
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithTcpServer(_settings.BrokerHost, _settings.BrokerPort)
            .WithClientId(_settings.ClientId);

        if (!string.IsNullOrEmpty(_settings.Username))
            builder.WithCredentials(_settings.Username, _settings.Password);

        await _client!.ConnectAsync(builder.Build(), cancellationToken);
    }

    private async Task DrainQueueAsync(CancellationToken cancellationToken)
    {
        if (_pendingQueue == null) return;

        while (_pendingQueue.Reader.TryRead(out var patientId))
        {
            if (cancellationToken.IsCancellationRequested) break;
            await PublishNowAsync(patientId);
        }
    }

    public async Task PublishPatientStateAsync(int patientId)
    {
        if (!_settings.Enabled) return;

        if (_client?.IsConnected == true)
        {
            await PublishNowAsync(patientId);
        }
        else if (_pendingQueue != null)
        {
            // Broker unavailable — queue for later. BoundedChannelFullMode.DropOldest
            // handles the case where the queue is full without blocking.
            if (!_pendingQueue.Writer.TryWrite(patientId))
                _logger.LogWarning("MQTT pending queue full — dropping oldest, queuing patient {PatientId}", patientId);
            else
                _logger.LogDebug("MQTT broker unavailable — patient {PatientId} queued ({Count} pending)", patientId, PendingCount);
        }
    }

    private async Task PublishNowAsync(int patientId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<PlsDbContext>();

            var patient = await context.Patients
                .Include(p => p.Body)
                .Include(p => p.QrCodePatient)
                .FirstOrDefaultAsync(p => p.Id == patientId);

            if (patient == null) return;

            var now = DateTime.UtcNow;

            // Versioned topic namespace — pls/v1/... allows future breaking changes on pls/v2/...
            // Payload includes schemaVersion for receivers to detect format changes.
            // Personal data (name, location) is omitted when RedactPersonalData is enabled.
            var payload = _settings.RedactPersonalData
                ? (object)new
                {
                    schemaVersion = 1,
                    eventTimestamp = now,
                    patientId = patient.Id,
                    triagefarbe = patient.Triagefarbe,
                    atmung = patient.Atmung,
                    blutung = patient.Blutung,
                    radialispuls = patient.Radialispuls,
                    transport = patient.Transport,
                    dringend = patient.Dringend,
                    kontaminiert = patient.Kontaminiert,
                    bodyParts = patient.Body?.BodyParts,
                    operationSceneId = patient.QrCodePatient?.OperationSceneId
                }
                : new
                {
                    schemaVersion = 1,
                    eventTimestamp = now,
                    patientId = patient.Id,
                    triagefarbe = patient.Triagefarbe,
                    atmung = patient.Atmung,
                    blutung = patient.Blutung,
                    radialispuls = patient.Radialispuls,
                    transport = patient.Transport,
                    dringend = patient.Dringend,
                    kontaminiert = patient.Kontaminiert,
                    name = patient.Name,
                    longitude = patient.LongitudePatient,
                    latitude = patient.LatitudePatient,
                    bodyParts = patient.Body?.BodyParts,
                    operationSceneId = patient.QrCodePatient?.OperationSceneId
                };

            var json = JsonSerializer.Serialize(payload);

            await _client!.PublishAsync(new MqttApplicationMessageBuilder()
                .WithTopic($"pls/v1/patients/{patientId}")
                .WithPayload(json)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(true)
                .Build());

            if (patient.QrCodePatient?.OperationSceneId != null)
            {
                var sceneId = patient.QrCodePatient.OperationSceneId;
                var scenePatients = await context.QrCodePatients
                    .Where(q => q.OperationSceneId == sceneId && q.PatientId != null)
                    .Select(q => q.PatientId)
                    .ToListAsync();

                await _client!.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic($"pls/v1/scenes/{sceneId}/patients")
                    .WithPayload(JsonSerializer.Serialize(new
                    {
                        schemaVersion = 1,
                        eventTimestamp = now,
                        sceneId,
                        patientIds = scenePatients
                    }))
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                    .WithRetainFlag(true)
                    .Build());
            }

            _logger.LogDebug("Published MQTT state for patient {PatientId}", patientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish MQTT state for patient {PatientId}", patientId);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _drainCts?.Cancel();
        _pendingQueue?.Writer.Complete();

        if (_client?.IsConnected == true)
            await _client.DisconnectAsync();
    }

    public void Dispose()
    {
        _drainCts?.Dispose();
        _client?.Dispose();
    }
}

/// <summary>
/// Health check that reports MQTT broker connectivity and pending queue depth.
/// </summary>
public class MqttHealthCheck : IHealthCheck
{
    private readonly MqttPublishService _mqttService;

    public MqttHealthCheck(MqttPublishService mqttService)
    {
        _mqttService = mqttService;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>
        {
            ["connected"] = _mqttService.IsConnected,
            ["pendingQueueDepth"] = _mqttService.PendingCount
        };

        if (!_mqttService.IsConnected)
            return Task.FromResult(HealthCheckResult.Degraded("MQTT broker is not connected", data: data));

        if (_mqttService.PendingCount > 0)
            return Task.FromResult(HealthCheckResult.Degraded($"MQTT pending queue has {_mqttService.PendingCount} entries", data: data));

        return Task.FromResult(HealthCheckResult.Healthy("MQTT connected", data));
    }
}
