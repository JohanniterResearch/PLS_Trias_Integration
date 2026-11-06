using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models;
using PLS.Api.Services;

namespace PLS.Api.Controllers;

[ApiController]
[Authorize(Policy = "TriageWrite")]
public class PatientChangesController(PlsDbContext db) : ControllerBase
{
    [HttpGet("api/v2/persons/{patientId}/changes")]
    public async Task<IActionResult> GetChanges(int patientId, [FromQuery] long? beforeId, [FromQuery] int limit = 50)
    {
        if (!await SceneAccess.CanAccessPatientAsync(db, User, patientId)) return Forbid();
        if (!await db.Patients.AnyAsync(p => p.Id == patientId)) return NotFound();
        if (limit is < 1 or > 100) return BadRequest(new { message = "limit muss zwischen 1 und 100 liegen" });
        var query = db.PatientChanges.AsNoTracking().Where(c => c.PatientId == patientId);
        if (beforeId.HasValue) query = query.Where(c => c.Id < beforeId.Value);
        var entries = await query.OrderByDescending(c => c.Id).Take(limit + 1).ToListAsync();
        var page = entries.Take(limit).ToList();
        return Ok(new
        {
            items = page.Select(c => new
            {
                id = c.Id, fieldId = c.FieldId, field = ChangeFields.Names.GetValueOrDefault(c.FieldId, "unknown"),
                value = JsonSerializer.Deserialize<JsonElement>(c.Value),
                changedAt = DateTime.SpecifyKind(c.ChangedAt, DateTimeKind.Utc),
                receivedAt = DateTime.SpecifyKind(c.ReceivedAt, DateTimeKind.Utc),
                accepted = c.Accepted, reason = c.Reason, actorId = c.ActorId, deviceId = c.DeviceId
            }),
            nextBeforeId = entries.Count > limit ? (long?)page[^1].Id : null
        });
    }
}
