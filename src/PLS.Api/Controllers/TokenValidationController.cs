using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PLS.Api.Controllers;

[ApiController]
public class TokenValidationController : ControllerBase
{
    [Authorize]
    [HttpPost("api/validate-token")]
    public IActionResult ValidateToken()
    {
        // actorType lets the frontend admin route guard verify it actually holds an
        // admin token instead of accepting any authenticated token.
        return Ok(new
        {
            isValid = true,
            actorType = User.FindFirst("type")?.Value,
            scene = int.TryParse(User.FindFirst("scene")?.Value, out var sceneId) ? sceneId : (int?)null
        });
    }
}
