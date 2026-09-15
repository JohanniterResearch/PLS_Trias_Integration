using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PLS.Api.Data;
using PLS.Api.Models.DTOs;
using PLS.Api.Models.Entities;

namespace PLS.Api.Services;

public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public int ExpirationInMinutes { get; set; } = 1440; // legacy — not used for new token types
    public string Issuer { get; set; } = "PLS";
    public int AdminExpirationMinutes { get; set; } = 60;
    public int QrExpirationMinutes { get; set; } = 480;
    public int UserExpirationMinutes { get; set; } = 480;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

public class AuthService
{
    private readonly PlsDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(PlsDbContext context, IOptions<JwtSettings> jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResult?> AuthenticateQrCode(string qrCode)
    {
        // Login QR codes are single-use bearer credentials (H-02). Consume them
        // atomically under a row lock so two concurrent scans can't both succeed —
        // same pattern as the patient-QR claim in PersonenV2Controller.
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<AuthResult?>(async () =>
        {
            // Clear stale tracked entities from a prior failed attempt before retrying —
            // otherwise identity resolution returns the previous attempt's mutated instance.
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var qrLogin = await _context.QrCodeLogins
                .FromSqlInterpolated($"SELECT * FROM qr_code_logins WHERE qr_login = {qrCode} FOR UPDATE")
                .AsTracking()
                .FirstOrDefaultAsync();

            if (qrLogin == null || qrLogin.FirstLogin != null) return null;

            qrLogin.FirstLogin = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // QR tokens: subject is the QrCodeLogin ID, no user security stamp needed.
            // No refresh tokens for QR logins — an expired session needs a new code.
            var accessToken = GenerateToken(qrLogin.Id.ToString(), "qr", null, _jwtSettings.QrExpirationMinutes);
            return new AuthResult(accessToken, string.Empty);
        });
    }

    public async Task<AdminAuthResult?> AuthenticateAdmin(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password)) return null;
        if (!user.AdminRole) return null;

        var accessToken = GenerateToken(user.Id.ToString(), "admin", user.SecurityStamp, _jwtSettings.AdminExpirationMinutes);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AdminAuthResult(accessToken, refreshToken, user.RequiresPasswordChange);
    }

    public async Task<AuthResult?> AuthenticateUser(string username, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password)) return null;

        // User role uses "qr" token type — intentional for testing phase (same TriageWrite access)
        var accessToken = GenerateToken(user.Id.ToString(), "qr", user.SecurityStamp, _jwtSettings.UserExpirationMinutes);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(accessToken, refreshToken);
    }

    // ponytail: dev-only, password-free login for the seeded accounts — callers must gate
    // this behind IsDevelopment() + a feature flag, same as PatientQrCodeController's dev endpoint.
    public async Task<AdminAuthResult?> DevLoginAdminAsync(string username)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && u.AdminRole);
        if (user == null) return null;

        var accessToken = GenerateToken(user.Id.ToString(), "admin", user.SecurityStamp, _jwtSettings.AdminExpirationMinutes);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AdminAuthResult(accessToken, refreshToken, user.RequiresPasswordChange);
    }

    public async Task<AuthResult?> DevLoginUserAsync(string username)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username && !u.AdminRole);
        if (user == null) return null;

        var accessToken = GenerateToken(user.Id.ToString(), "qr", user.SecurityStamp, _jwtSettings.UserExpirationMinutes);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        return new AuthResult(accessToken, refreshToken);
    }

    public async Task<AdminAuthResult?> RefreshAdminTokenAsync(string refreshToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<AdminAuthResult?>(async () =>
        {
            // Clear stale tracked entities from a prior failed attempt before retrying —
            // otherwise identity resolution returns the previous attempt's mutated instance.
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var stored = await LockRefreshTokenRowAsync(refreshToken);
            if (stored == null) return null;

            var user = await _context.Users.FirstAsync(u => u.Id == stored.UserId);
            if (!user.AdminRole) return null;

            stored.IsRevoked = true;
            var newRefresh = await CreateRefreshTokenAsync(stored.UserId);
            await transaction.CommitAsync();

            var accessToken = GenerateToken(stored.UserId.ToString(), "admin", user.SecurityStamp, _jwtSettings.AdminExpirationMinutes);
            return new AdminAuthResult(accessToken, newRefresh, user.RequiresPasswordChange);
        });
    }

    public async Task<AuthResult?> RefreshTokenAsync(string refreshToken)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync<AuthResult?>(async () =>
        {
            // Clear stale tracked entities from a prior failed attempt before retrying —
            // otherwise identity resolution returns the previous attempt's mutated instance.
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var stored = await LockRefreshTokenRowAsync(refreshToken);
            if (stored == null) return null;

            var user = await _context.Users.FirstAsync(u => u.Id == stored.UserId);

            stored.IsRevoked = true;
            var newRefresh = await CreateRefreshTokenAsync(stored.UserId);
            await transaction.CommitAsync();

            var type = user.AdminRole ? "admin" : "qr";
            var expiry = user.AdminRole ? _jwtSettings.AdminExpirationMinutes : _jwtSettings.UserExpirationMinutes;
            var accessToken = GenerateToken(stored.UserId.ToString(), type, user.SecurityStamp, expiry);
            return new AuthResult(accessToken, newRefresh);
        });
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken)
    {
        var hash = HashRefreshToken(refreshToken);
        var stored = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == hash);
        if (stored == null || stored.IsRevoked) return false;

        stored.IsRevoked = true;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Loads the refresh-token row under a FOR UPDATE lock so two concurrent refresh
    /// calls can't both consume the same token (double-redemption race).
    /// </summary>
    private async Task<RefreshToken?> LockRefreshTokenRowAsync(string refreshToken)
    {
        var hash = HashRefreshToken(refreshToken);
        var stored = await _context.RefreshTokens
            .FromSqlInterpolated($"SELECT * FROM RefreshTokens WHERE Token = {hash} FOR UPDATE")
            .AsTracking()
            .FirstOrDefaultAsync();

        if (stored == null || stored.IsRevoked || stored.ExpiresAt < DateTime.UtcNow) return null;
        return stored;
    }

    public async Task RevokeAllRefreshTokensForUserAsync(int userId)
    {
        var tokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
            t.IsRevoked = true;

        await _context.SaveChangesAsync();
    }

    private async Task<string> CreateRefreshTokenAsync(int userId)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            // Only the hash is stored — a database leak must not yield usable bearer tokens.
            Token = HashRefreshToken(token),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays)
        });
        await _context.SaveChangesAsync();
        return token;
    }

    private static string HashRefreshToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Re-issues the caller's token with a scene claim, binding the session to one
    /// operation scene. Patient endpoints require this claim for non-admin callers.
    /// The reissued token's expiry is capped at the presented token's own expiry
    /// (H-03) — otherwise scene selection becomes an unbounded session-renewal
    /// primitive that bypasses the refresh flow's rotation/revocation guarantees.
    /// </summary>
    public string MintSceneScopedToken(ClaimsPrincipal principal, int sceneId)
    {
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? string.Empty;
        var type = principal.FindFirst("type")?.Value ?? "qr";
        var securityStamp = principal.FindFirst("security_stamp")?.Value;
        var minutes = type == "admin" ? _jwtSettings.AdminExpirationMinutes : _jwtSettings.QrExpirationMinutes;

        var expiresAt = DateTime.UtcNow.AddMinutes(minutes);
        var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (long.TryParse(expClaim, out var expUnixSeconds))
        {
            var originalExpiry = DateTimeOffset.FromUnixTimeSeconds(expUnixSeconds).UtcDateTime;
            if (originalExpiry < expiresAt)
                expiresAt = originalExpiry;
        }

        return GenerateToken(subject, type, securityStamp, expiresAt, sceneId);
    }

    private string GenerateToken(string subject, string type, string? securityStamp, int expirationMinutes, int? sceneId = null) =>
        GenerateToken(subject, type, securityStamp, DateTime.UtcNow.AddMinutes(expirationMinutes), sceneId);

    private string GenerateToken(string subject, string type, string? securityStamp, DateTime expiresAt, int? sceneId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new("type", type),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (securityStamp != null)
            claims.Add(new Claim("security_stamp", securityStamp));

        if (sceneId != null)
            claims.Add(new Claim("scene", sceneId.Value.ToString()));

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Issuer,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
