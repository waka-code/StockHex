using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.AuthUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

/// <summary>
/// Cambio de la propia contraseña, pidiendo la actual.
///
/// Cambiar la contraseña <b>cierra todas las sesiones</b>: es lo que hace alguien
/// que cree que le robaron la cuenta, y dejar vivos los tokens de refresco anteriores
/// significaría que el atacante sigue dentro hasta catorce días después. Como eso
/// también mataría la sesión de quien la está cambiando, se emite un par nuevo y se
/// devuelve: el dispositivo desde el que se hizo el cambio continúa, los demás
/// quedan fuera.
/// </summary>
public sealed class ChangePassword
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IssueTokens _issueTokens;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePassword(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IssueTokens issueTokens,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _issueTokens = issueTokens;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> RunAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return Result<AuthResponse>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ConfirmPassword)] = ["Las contraseñas no coinciden."]
            }));

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result<AuthResponse>.Failure(Error.NotFound("Usuario", userId));

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result<AuthResponse>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.CurrentPassword)] = ["La contraseña actual es incorrecta."]
            }));

        if (request.NewPassword == request.CurrentPassword)
            return Result<AuthResponse>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.NewPassword)] = ["La contraseña nueva debe ser distinta de la actual."]
            }));

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);

        // Se revocan antes de emitir el par nuevo, para no revocar el que se acaba
        // de crear: los que devuelve GetActiveByUserAsync son los de antes.
        var now = DateTime.UtcNow;
        foreach (var token in await _refreshTokens.GetActiveByUserAsync(userId, cancellationToken))
        {
            token.Revoke(now, RevocationReasons.PasswordChanged);
            _refreshTokens.Update(token);
        }

        var (response, _) = await _issueTokens.RunAsync(user, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
