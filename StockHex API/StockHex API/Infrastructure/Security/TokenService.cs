using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateAccessToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            // El token lleva el ID del rol, no sus permisos: así quitar un permiso
            // surte efecto sin esperar a que el token se renueve. El nombre va
            // aparte y sólo para mostrar.
            new(StockHexClaims.RoleId, user.RoleId.ToString()),
            new(ClaimTypes.Role, user.Role?.Name ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>Tamaño del token de refresco: 256 bits de entropía criptográfica.</summary>
    private const int RefreshTokenBytes = 32;

    public RefreshTokenResult CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(RefreshTokenBytes);
        // Base64 url-safe para que el token viaje sin escapado en JSON o cabeceras.
        var token = Base64UrlEncoder.Encode(bytes);

        return new RefreshTokenResult(
            token,
            HashRefreshToken(token),
            DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

/// <summary>Nombres de claims registrados, para no depender de constantes de librería.</summary>
internal static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
    public const string Email = "email";
    public const string Jti = "jti";
}
