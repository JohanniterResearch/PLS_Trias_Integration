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

    public BodyPartController(PlsDbContext context, MqttPublishService mqttService)
    {
        _context = context;
        _mqttService = mqttService;
    }

    [Authorize(Policy = "TriageWrite")]
    [HttpPut("api/save-body-part")]
    public async Task<IActionResult> SaveBodyPart([FromBody] SaveBodyPartRequest request)
    {
        if (!BodyPartConstants.ValidKeys.Contains(request.bodyPartId))
            return BadRequest(new { error = "Invalid body part ID" });

        if (!await SceneAccess.CanAccessPatientAsync(_context, User, request.idpatient))
            return Forbid();

        var body = await _context.Bodies.FirstOrDefaultAsync(b => b.IdPatient == request.idpatient);
        if (body == null)
            return NotFound(new { error = "Body record not found" });

        body.BodyParts[request.bodyPartId] = request.isClicked;
        await _context.SaveChangesAsync();

        await _mqttService.PublishPatientStateAsync(request.idpatient);

        return Ok(new { message = "Body part saved successfully" });
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
