using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PLS.Api.Data;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;
using PLS.Api.Services;

namespace PLS.Api.Controllers;

[ApiController]
public class UserController : ControllerBase
{
    private readonly PlsDbContext _context;
    private readonly AuthService _authService;

    public UserController(PlsDbContext context, AuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/createAdminUser")]
    public async Task<IActionResult> CreateAdminUser([FromBody] CreateAdminUserRequest request)
    {
        var exists = await _context.Users.AnyAsync(u => u.Username == request.username);
        if (exists)
            return Conflict(new { error = "Benutzername existiert bereits" });

        var user = new User
        {
            Username = request.username,
            Password = BCrypt.Net.BCrypt.HashPassword(request.password),
            AdminRole = true,
            RequiresPasswordChange = true
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { status = "success", message = "Benutzer erfolgreich erstellt", user = new { user.Id, user.Username, user.AdminRole } });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("api/changeAdminPassword")]
    public async Task<IActionResult> ChangeAdminPassword([FromBody] ChangeAdminPasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.username);
        if (user == null)
            return NotFound(new { message = "User not found" });

        if (!BCrypt.Net.BCrypt.Verify(request.password, user.Password))
            return Unauthorized(new { message = "Invalid current password" });

        user.Password = BCrypt.Net.BCrypt.HashPassword(request.newpassword);
        user.RequiresPasswordChange = false;
        // Regenerate security stamp to immediately invalidate all existing access tokens
        user.SecurityStamp = Guid.NewGuid().ToString();
        await _context.SaveChangesAsync();

        // Revoke all outstanding refresh tokens for this user
        await _authService.RevokeAllRefreshTokensForUserAsync(user.Id);

        return Ok(new { status = "success", message = "User updated successfully", user = new { user.Id, user.Username, user.AdminRole } });
    }
}
