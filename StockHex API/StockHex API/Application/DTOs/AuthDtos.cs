namespace StockHex_API.Application.DTOs;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(
    string Name,
    string Email,
    string Password,
    string ConfirmPassword);

/// <summary>Canje de un token de refresco por un nuevo par de tokens.</summary>
public sealed record RefreshTokenRequest(string RefreshToken);

/// <summary>
/// Cierre de sesión. Con <c>AllSessions = true</c> revoca todos los tokens
/// vigentes del usuario, no sólo el de este dispositivo.
/// </summary>
public sealed record LogoutRequest(string RefreshToken, bool AllSessions = false);

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserResponse User);
