using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;

namespace PLS.Api.Services;

public class PatientChangeService(PlsDbContext db)
{
    public async Task<IActionResult> ExecuteAsync(int patientId, Func<Patient, Task<IActionResult>> action)
    {
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // One common lock also protects fields stored in JSON and first-time form creation.
            // Clear tracked state before retries so a rolled-back attempt cannot leak into the next.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var patient = await db.Patients
                .FromSqlInterpolated($"SELECT * FROM patients WHERE id = {patientId} FOR UPDATE")
                .AsTracking().FirstOrDefaultAsync();
            if (patient == null) return (IActionResult)new NotFoundObjectResult(new { message = "Person nicht gefunden" });
            var response = await action(patient);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return response;
        });
    }

    public ChangeSession Start(int patientId, ChangeMetadata metadata, ClaimsPrincipal user) => new(db, patientId, metadata, user);
}

public class ChangeSession(PlsDbContext db, int patientId, ChangeMetadata metadata, ClaimsPrincipal user)
{
    private readonly DateTime receivedAt = DateTime.UtcNow;
    public List<RejectedChange> Rejected { get; } = new();

    public async Task<bool> ApplyAsync(string field, object? current, object? requested, DateTime? baseline, string? blockedReason = null)
    {
        var fieldId = ChangeFields.Ids[field];
        var timestamp = metadata.FieldChangedAt != null && metadata.FieldChangedAt.TryGetValue(field, out var fieldTime)
            ? fieldTime.UtcDateTime : metadata.ChangedAt?.UtcDateTime ?? receivedAt;
        var changedAt = NormalizeTime(timestamp);
        var previousJson = JsonSerializer.Serialize(current);
        var requestedJson = JsonSerializer.Serialize(requested);
        var latestTime = await db.PatientChanges.AsNoTracking()
            .Where(c => c.PatientId == patientId && c.FieldId == fieldId && c.Accepted)
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id)
            .Select(c => (DateTime?)c.ChangedAt).FirstOrDefaultAsync();
        var previousTime = latestTime ?? baseline;
        if (previousTime.HasValue) previousTime = DateTime.SpecifyKind(previousTime.Value, DateTimeKind.Utc);
        // The patient lock also protects this duplicate check; JSON values are compared structurally.
        var candidates = await db.PatientChanges.AsNoTracking()
            .Where(c => c.PatientId == patientId && c.FieldId == fieldId && c.ChangedAt == changedAt)
            .ToListAsync();
        var replay = candidates.FirstOrDefault(c =>
            JsonNode.DeepEquals(JsonNode.Parse(c.Value), JsonNode.Parse(requestedJson)));
        var sameValue = JsonNode.DeepEquals(JsonNode.Parse(previousJson), JsonNode.Parse(requestedJson));
        var reason = Decide(changedAt, previousTime, sameValue, blockedReason);
        if (replay != null)
        {
            // A retry needs no new audit row, but a formerly accepted value may now be superseded.
            var replayReason = replay.Accepted ? reason : replay.Reason;
            if (replayReason is not ("accepted" or "unchanged"))
                Rejected.Add(new(fieldId, field, replayReason, current, previousTime));
            return false;
        }

        var accepted = reason == "accepted";

        // An identical retry needs no extra row. A newer identical value still advances its field clock.
        if (reason == "unchanged") return false;
        db.PatientChanges.Add(new PatientChange
        {
            PatientId = patientId, FieldId = fieldId,
            Value = requestedJson,
            ChangedAt = changedAt, ReceivedAt = receivedAt,
            Accepted = accepted, Reason = reason,
            ActorId = $"{user.FindFirstValue("type")}:{user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub")}",
            DeviceId = metadata.DeviceId
        });
        if (!accepted) Rejected.Add(new(fieldId, field, reason, current, previousTime));
        return accepted;
    }

    // Match MySQL datetime(6) before comparing, including retries carrying .NET's seventh digit.
    public static DateTime NormalizeTime(DateTime value) =>
        DateTime.SpecifyKind(value.AddTicks(-(value.Ticks % 10)), DateTimeKind.Utc);

    public static string Decide(DateTime incoming, DateTime? stored, bool sameValue, string? blockedReason = null)
    {
        incoming = NormalizeTime(incoming);
        if (stored.HasValue) stored = NormalizeTime(stored.Value);
        if (blockedReason != null) return sameValue ? "unchanged" : blockedReason;
        if (stored == null || incoming > stored) return "accepted";
        if (sameValue) return "unchanged";
        return incoming == stored ? "equal_timestamp" : "older_timestamp";
    }
}
