using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>
/// Revoca el token de refresco de este dispositivo o, con <c>AllSessions</c>,
/// todos los del usuario. El access token en curso sigue siendo válido hasta
/// que expire: es la contrapartida de no consultar la base en cada petición.
/// </summary>
public sealed class Logout
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public Logout(
        IRefreshTokenRepository refreshTokens,
        ITokenService tokenService,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _refreshTokens = refreshTokens;
        _tokenService = tokenService;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return Result.Failure(Error.Validation("El token de refresco es obligatorio."));

        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken);

        if (stored is null)
            return Result.Failure(Error.NotFound("El token de refresco no existe."));

        // Sin esta comprobación, cualquier usuario autenticado podría cerrar la
        // sesión de otro con sólo conocer su token.
        if (_currentUser.Id is not null && stored.UserId != _currentUser.Id)
            return Result.Failure(Error.Forbidden("El token no pertenece al usuario autenticado."));

        var now = DateTime.UtcNow;

        if (request.AllSessions)
        {
            foreach (var token in await _refreshTokens.GetActiveByUserAsync(stored.UserId, cancellationToken))
            {
                token.Revoke(now, RevocationReasons.LoggedOut);
                _refreshTokens.Update(token);
            }
        }
        else if (stored.RevokedAt is null)
        {
            stored.Revoke(now, RevocationReasons.LoggedOut);
            _refreshTokens.Update(stored);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
