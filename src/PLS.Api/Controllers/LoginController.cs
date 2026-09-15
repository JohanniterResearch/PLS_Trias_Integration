using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PLS.Api.Models.DTOs;
using PLS.Api.Services;

namespace PLS.Api.Controllers;

[ApiController]
public class LoginController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ILogger<LoginController> _logger;
    private readonly MetricsService _metrics;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly BootstrapSettings _bootstrap;

    public LoginController(
        AuthService authService,
        ILogger<LoginController> logger,
        MetricsService metrics,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IOptions<BootstrapSettings> bootstrap)
    {
        _authService = authService;
        _logger = logger;
        _metrics = metrics;
        _environment = environment;
        _configuration = configuration;
        _bootstrap = bootstrap.Value;
    }

    [EnableRateLimiting("auth")]
    [HttpPost("api/qr-login")]
    public async Task<IActionResult> QrLogin([FromBody] QrLoginRequest request)
    {
        var result = await _authService.AuthenticateQrCode(request.qr_code);
        if (result == null)
        {
            _metrics.IncrementAuthFailures();
            _logger.LogWarning("QR login failed for code prefix {Prefix} from {IP}",
                request.qr_code.Length > 8 ? request.qr_code[..8] : "?",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { status = "error", message = "Invalid QR code" });
        }

        return Ok(new { status = "success", token = result.AccessToken });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("api/user-login")]
    public async Task<IActionResult> UserLogin([FromBody] AdminLoginRequest request)
    {
        var result = await _authService.AuthenticateUser(request.username, request.password);
        if (result == null)
        {
            _metrics.IncrementAuthFailures();
            _logger.LogWarning("User login failed for {Username} from {IP}",
                request.username, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { status = "error", message = "Invalid credentials" });
        }

        return Ok(new { status = "success", token = result.AccessToken, refreshToken = result.RefreshToken });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("api/adminLogin")]
    public async Task<IActionResult> AdminLogin([FromBody] AdminLoginRequest request)
    {
        var result = await _authService.AuthenticateAdmin(request.username, request.password);
        if (result == null)
        {
            _metrics.IncrementAuthFailures();
            _logger.LogWarning("Admin login failed for {Username} from {IP}",
                request.username, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { status = "error", message = "Invalid credentials" });
        }

        return Ok(new
        {
            status = "success",
            token = result.AccessToken,
            refreshToken = result.RefreshToken,
            requiresPasswordChange = result.RequiresPasswordChange
        });
    }

    [EnableRateLimiting("auth")]
    [HttpPost("api/refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // Try admin refresh first, then generic (user role)
        var adminResult = await _authService.RefreshAdminTokenAsync(request.refreshToken);
        if (adminResult != null)
            return Ok(new
            {
                status = "success",
                token = adminResult.AccessToken,
                refreshToken = adminResult.RefreshToken,
                requiresPasswordChange = adminResult.RequiresPasswordChange
            });

        var result = await _authService.RefreshTokenAsync(request.refreshToken);
        if (result == null)
            return Unauthorized(new { status = "error", message = "Invalid or expired refresh token" });

        return Ok(new { status = "success", token = result.AccessToken, refreshToken = result.RefreshToken });
    }

    // DEV WORKAROUND — remove before production
    [HttpPost("api/dev-login")]
    public async Task<IActionResult> DevLogin([FromBody] DevLoginRequest request)
    {
        // Explicit opt-in only, and never outside development — same gate as the
        // PatientQrCodeController dev QR helper.
        var isEnabled = _environment.IsDevelopment()
                        && _configuration.GetValue<bool>("Features:EnableDevLogin");
        if (!isEnabled)
            return NotFound();

        if (request.role == "admin")
        {
            var result = await _authService.DevLoginAdminAsync(_bootstrap.AdminUsername);
            if (result == null)
                return NotFound(new { status = "error", message = "Seeded admin account not found" });

            return Ok(new
            {
                status = "success",
                token = result.AccessToken,
                refreshToken = result.RefreshToken,
                requiresPasswordChange = result.RequiresPasswordChange
            });
        }

        if (request.role == "user")
        {
            var result = await _authService.DevLoginUserAsync(_bootstrap.TestUserUsername);
            if (result == null)
                return NotFound(new { status = "error", message = "Seeded test user not found" });

            return Ok(new { status = "success", token = result.AccessToken, refreshToken = result.RefreshToken });
        }

        return BadRequest(new { status = "error", message = "role must be 'admin' or 'user'" });
    }

    [Authorize]
    [HttpPost("api/logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await _authService.RevokeRefreshTokenAsync(request.refreshToken);
        return Ok(new { status = "success", message = "Logged out" });
    }
}
