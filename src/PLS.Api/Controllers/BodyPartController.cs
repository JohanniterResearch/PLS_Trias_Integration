using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;
using PLS.Api.Services;

namespace PLS.Api.Controllers;

[ApiController]
public class BodyPartController : ControllerBase
{
    private readonly PlsDbContext _context;
    private readonly MqttPublishService _mqttService;
    private readonly PatientChangeService _changes;

    public BodyPartController(PlsDbContext context, MqttPublishService mqttService, PatientChangeService changes)
    {
        _context = context;
        _mqttService = mqttService;
        _changes = changes;
    }

    [Authorize(Policy = "TriageWrite")]
    [HttpPut("api/save-body-part")]
    public async Task<IActionResult> SaveBodyPart([FromBody] SaveBodyPartRequest request)
    {
        if (!BodyPartConstants.ValidKeys.Contains(request.bodyPartId))
            return BadRequest(new { error = "Invalid body part ID" });

        if (!await SceneAccess.CanAccessPatientAsync(_context, User, request.idpatient))
            return Forbid();

        var result = await _changes.ExecuteAsync(request.idpatient, async patient =>
        {
            var body = await _context.Bodies.FirstOrDefaultAsync(b => b.IdPatient == request.idpatient);
            if (body == null) return NotFound(new { error = "Body record not found" });
            var session = _changes.Start(request.idpatient, request, User);
            if (await session.ApplyAsync("body." + request.bodyPartId, body.BodyParts.GetValueOrDefault(request.bodyPartId), request.isClicked!.Value, patient.CreatedAt))
                body.BodyParts[request.bodyPartId] = request.isClicked.Value;
            return Ok(new
            {
                message = "Body part processed", rejectedChanges = session.Rejected,
                bodyPartId = request.bodyPartId, isClicked = body.BodyParts.GetValueOrDefault(request.bodyPartId)
            });
        });
        if (result is OkObjectResult) await _mqttService.PublishPatientStateAsync(request.idpatient);
        return result;
    }

    [Authorize(Policy = "TriageWrite")]
    [HttpGet("api/get-body-parts")]
    public async Task<IActionResult> GetBodyParts([FromQuery] int idpatient)
    {
        if (!await SceneAccess.CanAccessPatientAsync(_context, User, idpatient))
            return Forbid();

        var body = await _context.Bodies.FirstOrDefaultAsync(b => b.IdPatient == idpatient);
        if (body == null)
            return NotFound(new { error = "Error fetching body parts" });

        // Return flat JSON matching old column-based format
        // Include idbody and idpatient like the old Laravel response
        var result = new Dictionary<string, object>
        {
            ["idbody"] = body.Id,
            ["idpatient"] = body.IdPatient,
            ["created_at"] = body.CreatedAt!,
            ["updated_at"] = body.UpdatedAt!
        };

        foreach (var kvp in body.BodyParts)
        {
            result[kvp.Key] = kvp.Value;
        }

        return Ok(result);
    }
}
