using StockHex_API.Domain.Entities;

namespace StockHex_API.Application.Abstractions;

/// <summary>Token de refresco recién emitido: el valor en claro va al cliente, el hash a la base.</summary>
public sealed record RefreshTokenResult(string Token, string TokenHash, DateTime ExpiresAt);

public interface ITokenService
{
    /// <summary>Genera el JWT del usuario junto con su instante de expiración en UTC.</summary>
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);

    /// <summary>Genera un token de refresco aleatorio con su hash y su vencimiento.</summary>
    RefreshTokenResult CreateRefreshToken();

    /// <summary>Hash del token tal como se almacena; se usa para buscar el que envía el cliente.</summary>
    string HashRefreshToken(string token);
}
