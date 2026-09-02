using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>
/// Devuelve el perfil del portador del token con sus permisos efectivos, para que
/// el frontend no tenga que cachearlos ni deducirlos del nombre del rol.
/// </summary>
public sealed class GetCurrentUser
{
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionResolver _permissions;

    public GetCurrentUser(
        IUserRepository users,
        ICurrentUser currentUser,
        IPermissionResolver permissions)
    {
        _users = users;
        _currentUser = currentUser;
        _permissions = permissions;
    }

    public async Task<Result<CurrentUserResponse>> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.Id is null)
            return Result<CurrentUserResponse>.Failure(Error.Unauthorized("No hay un usuario autenticado."));

        var user = await _users.GetByIdAsync(_currentUser.Id.Value, cancellationToken);
        if (user is null)
            return Result<CurrentUserResponse>.Failure(
                Error.Unauthorized("El usuario del token ya no existe."));

        // Se resuelven en el momento: si le cambiaron el rol o los permisos, esta
        // llamada ya devuelve los nuevos sin esperar a renovar el token.
        var permissions = await _permissions.GetForRoleAsync(user.RoleId, cancellationToken);

        return user.ToCurrentUser(permissions.OrderBy(p => p, StringComparer.Ordinal).ToList());
    }
}
