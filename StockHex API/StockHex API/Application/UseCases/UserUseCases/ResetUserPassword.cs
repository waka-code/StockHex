using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

/// <summary>
/// Restablece la contraseña de OTRO usuario. No pide la actual, porque quien la
/// cambia no la conoce; por eso exige el permiso <c>users.change_password</c> y,
/// opcionalmente, revoca las sesiones del afectado.
/// </summary>
public sealed class ResetUserPassword
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ResetUserPassword(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return Result.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ConfirmPassword)] = ["Las contraseñas no coinciden."],
            }));

        // Para la propia cuenta existe el endpoint que sí pide la contraseña actual;
        // permitir el atajo aquí sería una forma de saltárselo.
        if (_currentUser.Id == userId)
            return Result.Failure(Error.Conflict(
                "Para cambiar tu propia contraseña usa el cambio con contraseña actual."));

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("Usuario", userId));

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        _users.Update(user);

        if (request.RevokeSessions)
        {
            // Sin esto, quien tuviera la sesión abierta seguiría dentro con la
            // contraseña anterior hasta que su refresco caducara.
            var now = DateTime.UtcNow;
            foreach (var token in await _refreshTokens.GetActiveByUserAsync(userId, cancellationToken))
            {
                token.Revoke(now, RevocationReasons.UserDisabled);
                _refreshTokens.Update(token);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
