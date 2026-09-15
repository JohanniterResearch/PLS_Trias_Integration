using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;
using PLS.Api.Services;

namespace PLS.Api.Controllers;

/// <summary>
/// Task 53: Clean v2 operation scene endpoints.
/// Differences from v1 (OperationSceneController):
///  - Response key is "operationScene" (correct spelling).
///    v1 uses "operactionScene" (intentional typo for frontend compatibility).
/// </summary>
[ApiController]
public class OperationSceneV2Controller : ControllerBase
{
    private readonly PlsDbContext _context;
    private readonly AuthService _authService;

    public OperationSceneV2Controller(PlsDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    /// <summary>
    /// Binds the caller's session to one operation scene by re-issuing the token
    /// with a scene claim. Patient endpoints require this claim for non-admin callers.
    /// </summary>
    [Authorize(Policy = "QrOrAdmin")]
    [HttpPost("api/v2/select-scene")]
    public async Task<IActionResult> SelectScene([FromBody] SelectSceneRequest request)
    {
        var sceneExists = await _context.OperationScenes.AnyAsync(scene => scene.Id == request.operationSceneId);
        if (!sceneExists)
            return NotFound(new { message = "Einsatzort nicht gefunden" });

        var token = _authService.MintSceneScopedToken(User, request.operationSceneId);
        return Ok(new { status = "success", token });
    }

    /// <summary>
    /// Creates or updates an operation scene.
    /// Response uses "operationScene" (correct spelling, unlike v1's "operactionScene").
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/v2/createOperationScene")]
    public async Task<IActionResult> CreateOperationScene([FromBody] CreateOperationSceneRequest request)
    {
        OperationScene operationScene;

        if (request.id.HasValue)
        {
            // An explicit id is an update — silently creating a new scene here
            // hid client-side id mistakes.
            var existing = await _context.OperationScenes.FindAsync(request.id.Value);
            if (existing == null)
                return NotFound(new { message = "Einsatzort nicht gefunden" });
            operationScene = existing;
        }
        else
        {
            operationScene = new OperationScene();
            _context.OperationScenes.Add(operationScene);
        }

        operationScene.Name = request.name;
        operationScene.Description = request.description;
        await _context.SaveChangesAsync();

        // v2: correctly spelled "operationScene" key (v1 has "operactionScene" typo)
        return Ok(new
        {
            status = "success",
            operationScene = new
            {
                idoperationScene = operationScene.Id,
                name = operationScene.Name,
                description = operationScene.Description,
                created_at = operationScene.CreatedAt,
                updated_at = operationScene.UpdatedAt
            }
        });
    }

    /// <summary>Returns operation scenes updated in the last 20 days.</summary>
    [Authorize(Policy = "QrOrAdmin")]
    [HttpGet("api/v2/getAllCurrentOperationScenes")]
    public async Task<IActionResult> GetAllCurrentOperationScenes()
    {
        var twentyDaysAgo = DateTime.UtcNow.AddDays(-20);

        var scenes = await _context.OperationScenes
            .Where(s => s.UpdatedAt >= twentyDaysAgo)
            .Select(s => new
            {
                idoperationScene = s.Id,
                name = s.Name,
                description = s.Description,
                created_at = s.CreatedAt,
                updated_at = s.UpdatedAt
            })
            .ToListAsync();

        return Ok(scenes);
    }

    /// <summary>Deletes an operation scene by ID.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("api/v2/deleteOperationScene/{id}")]
    public async Task<IActionResult> DeleteOperationScene(int id)
    {
        var operationScene = await _context.OperationScenes.FindAsync(id);
        if (operationScene == null)
            return NotFound(new { status = "error", message = "Einsatzort nicht gefunden" });

        _context.OperationScenes.Remove(operationScene);
        await _context.SaveChangesAsync();

        return Ok(new { status = "success" });
    }
}
