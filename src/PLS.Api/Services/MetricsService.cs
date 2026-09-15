using System.Threading;

namespace PLS.Api.Services;

/// <summary>
/// Task 60: In-process metrics counters for triage-critical signals.
/// Exposed via GET /metrics (AdminOnly). Resets on app restart — not a persistence store.
/// For production monitoring, scrape /health for MQTT state and /metrics for counters.
/// </summary>
public class MetricsService
{
    private long _authFailures;
    private long _totalRequests;
    private long _dbErrors;

    public void IncrementAuthFailures() => Interlocked.Increment(ref _authFailures);
    public void IncrementRequests() => Interlocked.Increment(ref _totalRequests);
    public void IncrementDbErrors() => Interlocked.Increment(ref _dbErrors);

    public MetricsSnapshot Snapshot() => new(
        AuthFailures: Interlocked.Read(ref _authFailures),
        TotalRequests: Interlocked.Read(ref _totalRequests),
        DbErrors: Interlocked.Read(ref _dbErrors));
}

public record MetricsSnapshot(
    long AuthFailures,
    long TotalRequests,
    long DbErrors);
