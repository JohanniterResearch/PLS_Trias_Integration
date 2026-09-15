using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLS.Api.Services;

namespace PLS.Api.Controllers;

/// <summary>
/// Task 60: Metrics endpoint exposing in-process counters and MQTT health state.
/// AdminOnly — not publicly accessible.
/// </summary>
[ApiController]
public class MetricsController : ControllerBase
{
    private readonly MetricsService _metrics;
    private readonly MqttPublishService _mqtt;

    public MetricsController(MetricsService metrics, MqttPublishService mqtt)
    {
        _metrics = metrics;
        _mqtt = mqtt;
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("api/metrics")]
    public IActionResult GetMetrics()
    {
        var snap = _metrics.Snapshot();
        return Ok(new
        {
            authFailures = snap.AuthFailures,
            totalRequests = snap.TotalRequests,
            dbErrors = snap.DbErrors,
            mqtt = new
            {
                connected = _mqtt.IsConnected,
                pendingQueueDepth = _mqtt.PendingCount
            }
        });
    }
}
