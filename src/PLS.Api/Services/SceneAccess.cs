using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;

namespace PLS.Api.Services;

/// <summary>
/// Object-level authorization for operation scenes (C-05): admin tokens see every
/// scene; qr/user tokens must carry a scene claim (minted via api/v2/select-scene)
/// matching the requested scene or the target patient's scene.
/// </summary>
public static class SceneAccess
{
    public static bool CanAccessScene(ClaimsPrincipal principal, int sceneId)
    {
        if (principal.FindFirst("type")?.Value == "admin") return true;
        return GetSceneClaim(principal) == sceneId;
    }

    public static async Task<bool> CanAccessPatientAsync(PlsDbContext db, ClaimsPrincipal principal, int patientId)
    {
        if (principal.FindFirst("type")?.Value == "admin") return true;

        var sceneId = GetSceneClaim(principal);
        if (sceneId == null) return false;

        return await db.QrCodePatients.AnyAsync(q => q.PatientId == patientId && q.OperationSceneId == sceneId);
    }

    private static int? GetSceneClaim(ClaimsPrincipal principal)
    {
        return int.TryParse(principal.FindFirst("scene")?.Value, out var sceneId) ? sceneId : null;
    }
}
