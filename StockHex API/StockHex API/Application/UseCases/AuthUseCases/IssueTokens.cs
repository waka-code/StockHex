using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>
/// Emite el par access + refresh y persiste el refresco. Lo comparten login,
/// registro y renovación para que el formato de <see cref="AuthResponse"/> y la
/// política de expiración se definan en un único sitio.
/// </summary>
public sealed class IssueTokens
{
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPermissionResolver _permissions;

    public IssueTokens(
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokens,
        IPermissionResolver permissions)
    {
        _tokenService = tokenService;
        _refreshTokens = refreshTokens;
        _permissions = permissions;
    }

    /// <summary>
    /// No guarda los cambios: quien la llama decide cuándo confirmar, de modo que
    /// la emisión del token y el resto de la operación queden en la misma transacción.
    /// </summary>
    public async Task<(AuthResponse Response, RefreshToken Token)> RunAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var (accessToken, accessExpiresAt) = _tokenService.CreateAccessToken(user);
        var refresh = _tokenService.CreateRefreshToken();

        var entity = new RefreshToken
        {
            TokenHash = refresh.TokenHash,
            UserId = user.Id,
            ExpiresAt = refresh.ExpiresAt,
        };

        await _refreshTokens.AddAsync(entity, cancellationToken);

        // Los permisos van en el cuerpo, no en el token: el frontend los usa para no
        // ofrecer acciones que van a fallar, y se releen en cada renovación.
        var permissions = await _permissions.GetForRoleAsync(user.RoleId, cancellationToken);

        var response = new AuthResponse(
            accessToken,
            accessExpiresAt,
            refresh.Token,
            refresh.ExpiresAt,
            user.ToCurrentUser(permissions.OrderBy(p => p, StringComparer.Ordinal).ToList()));

        return (response, entity);
    }
}
