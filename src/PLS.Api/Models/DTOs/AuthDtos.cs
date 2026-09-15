namespace PLS.Api.Models.DTOs;

public record QrLoginRequest(string qr_code);
public record AdminLoginRequest(string username, string password);
public record AuthResponse(string status, string? token = null, string? message = null);

// Rich auth result types used internally between AuthService and controllers
public record AuthResult(string AccessToken, string RefreshToken);
public record AdminAuthResult(string AccessToken, string RefreshToken, bool RequiresPasswordChange);

public record RefreshTokenRequest(string refreshToken);
public record LogoutRequest(string refreshToken);
public record DevLoginRequest(string role);
