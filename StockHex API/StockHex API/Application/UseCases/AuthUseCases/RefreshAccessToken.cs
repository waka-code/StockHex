using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>
/// Canjea un token de refresco por un par nuevo. Aplica <b>rotación</b>: el token
/// usado queda revocado y se emite otro. Si llega un token ya rotado, se asume
/// que fue robado y se invalida toda la cadena de esa sesión.
/// </summary>
public sealed class RefreshAccessToken
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly ITokenService _tokenService;
    private readonly IssueTokens _issueTokens;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshAccessToken(
        IRefreshTokenRepository refreshTokens,
        IUserRepository users,
        ITokenService tokenService,
        IssueTokens issueTokens,
        IUnitOfWork unitOfWork)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _tokenService = tokenService;
        _issueTokens = issueTokens;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> RunAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result<AuthResponse>.Failure(
                Error.Unauthorized("El token de refresco es obligatorio."));

        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        // Mensaje único para token inexistente, expirado o revocado: no se le da
        // pistas a quien esté probando tokens.
        const string invalid = "El token de refresco no es válido o expiró.";

        if (stored is null)
            return Result<AuthResponse>.Failure(Error.Unauthorized(invalid));

        var now = DateTime.UtcNow;

        if (!stored.IsActive(now))
        {
            // Un token revocado que vuelve a aparecer indica que alguien tiene una
            // copia: se corta la sesión completa en lugar de sólo rechazar la petición.
            if (stored.RevokedAt is not null)
                await RevokeChainAsync(stored, cancellationToken);

            return Result<AuthResponse>.Failure(Error.Unauthorized(invalid));
        }

        var user = stored.User ?? await _users.GetByIdAsync(stored.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            stored.Revoke(now, RevocationReasons.UserDisabled);
            _refreshTokens.Update(stored);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result<AuthResponse>.Failure(Error.Unauthorized("La cuenta está desactivada."));
        }

        var (response, issued) = await _issueTokens.RunAsync(user, cancellationToken);

        stored.Revoke(now, RevocationReasons.Rotated);
        stored.ReplacedByTokenId = issued.Id;
        _refreshTokens.Update(stored);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }

    private async Task RevokeChainAsync(RefreshToken reused, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var chain = await _refreshTokens.GetChainAsync(reused.Id, cancellationToken);

        foreach (var token in chain.Where(t => t.RevokedAt is null))
        {
            token.Revoke(now, RevocationReasons.Reused);
            _refreshTokens.Update(token);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
