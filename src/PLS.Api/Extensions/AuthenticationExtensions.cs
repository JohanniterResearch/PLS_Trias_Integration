using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PLS.Api.Data;
using PLS.Api.Services;

namespace PLS.Api.Extensions;

public static class HttpContextItemKeys
{
    /// <summary>
    /// Set by <see cref="AuthenticationExtensions"/>'s OnTokenValidated for
    /// user-account-backed tokens, so downstream middleware (the forced
    /// password-change gate) can reuse the row instead of re-querying it.
    /// </summary>
    public const string CurrentUser = "CurrentUser";
}

public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()!;

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
            throw new InvalidOperationException(
                "JWT secret is not configured. Set Jwt:Secret through configuration.");

        if (jwtSettings.Secret.Length < 32)
            throw new InvalidOperationException(
                "JWT secret must be at least 32 characters.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Issuer,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
            };

            // Validate SecurityStamp on every request for user-account-backed tokens.
            // QR login tokens (subject = QrCodeLogin.Id, no security_stamp claim) are skipped.
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var stampClaim = context.Principal?.FindFirst("security_stamp")?.Value;
                    if (stampClaim == null) return; // QR tokens — skip stamp check

                    var subClaim =
                        context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                        ?? context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (!int.TryParse(subClaim, out var userId))
                    {
                        context.Fail("Invalid token subject");
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<PlsDbContext>();
                    var user = await db.Users.FindAsync(userId);
                    if (user == null || user.SecurityStamp != stampClaim)
                    {
                        context.Fail("Token has been invalidated (security stamp mismatch)");
                        return;
                    }

                    context.HttpContext.Items[HttpContextItemKeys.CurrentUser] = user;
                }
            };
        });

        return services;
    }
}
