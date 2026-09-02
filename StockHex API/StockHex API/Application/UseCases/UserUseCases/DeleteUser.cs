using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class DeleteUser
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly ICurrentUser _currentUser;
    private readonly IActiveUserResolver _activeUsers;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUser(
        IUserRepository users,
        IRoleRepository roles,
        ICurrentUser currentUser,
        IActiveUserResolver activeUsers,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _roles = roles;
        _currentUser = currentUser;
        _activeUsers = activeUsers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("Usuario", id));

        if (_currentUser.Id == id)
            return Result.Failure(Error.Conflict("Un usuario no puede eliminar su propia cuenta."));

        // Igual que en el update: lo que se protege es que quede alguien capaz de
        // administrar, no un nombre de rol concreto.
        foreach (var permission in Permissions.Critical)
        {
            var others = await _roles.CountActiveUsersWithPermissionAsync(
                permission, excludingRoleId: user.RoleId, cancellationToken);

            if (others > 0)
                continue;

            var sameRole = await _users.CountActiveByRoleAsync(user.RoleId, cancellationToken);
            if (sameRole > 1)
                continue;

            return Result.Failure(Error.Conflict(
                $"Eliminar este usuario dejaría el sistema sin nadie con '{permission}'."));
        }

        // Los movimientos referencian al usuario con FK restrictiva, así que se desactiva
        // en lugar de borrar cuando ya registró actividad.
        var movementCount = await _users.CountMovementsAsync(id, cancellationToken);

        if (movementCount > 0 || !user.IsActive)
        {
            if (!user.IsActive)
                return Result.Failure(Error.Conflict("El usuario ya está desactivado."));

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            _users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _activeUsers.Invalidate(user.Id);

            return Result.Failure(Error.Conflict(
                $"El usuario registró {movementCount} movimiento(s) de inventario, por lo que " +
                "se desactivó en lugar de eliminarse para conservar la auditoría."));
        }

        _users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Su token sigue firmado y sin expirar; lo que lo invalida es que el
        // resolver deje de encontrar la cuenta.
        _activeUsers.Invalidate(user.Id);

        return Result.Success();
    }
}
