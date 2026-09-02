using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class UpdateUser
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUser(IUserRepository users, IRoleRepository roles, IUnitOfWork unitOfWork)
    {
        _users = users;
        _roles = roles;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> RunAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return Result<UserResponse>.Failure(Error.NotFound("Usuario", id));

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(email, id, cancellationToken))
            return Result<UserResponse>.Failure(
                Error.Conflict($"Ya existe otro usuario con el email '{email}'."));

        var role = await _roles.GetByIdAsync(request.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
            return Result<UserResponse>.Failure(Error.NotFound("Rol", request.RoleId));

        // Con roles configurables, «el último admin» ya no es un valor sino una
        // capacidad: lo que hay que preservar es que quede alguien activo capaz de
        // administrar roles y usuarios.
        var guard = await GuardCriticalAccessAsync(user.Id, user.RoleId, role.Id,
            request.IsActive, cancellationToken);
        if (guard is not null)
            return Result<UserResponse>.Failure(guard);

        user.Name = request.Name.Trim();
        user.Email = email;
        user.RoleId = role.Id;
        user.Role = role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.ToResponse();
    }

    /// <summary>
    /// Devuelve un error si el cambio dejaría el sistema sin ningún usuario activo
    /// con los permisos críticos. Null si no hay problema.
    /// </summary>
    private async Task<Error?> GuardCriticalAccessAsync(
        Guid userId,
        Guid currentRoleId,
        Guid newRoleId,
        bool willBeActive,
        CancellationToken cancellationToken)
    {
        var newRole = await _roles.GetByIdAsync(newRoleId, includePermissions: true, cancellationToken);

        foreach (var permission in Permissions.Critical)
        {
            var keepsIt = willBeActive && (newRole?.Grants(permission) ?? false);
            if (keepsIt)
                continue;

            // Se excluye el rol actual del usuario para no contarlo a él mismo,
            // y se comprueba si alguien más conserva el permiso.
            var others = await _roles.CountActiveUsersWithPermissionAsync(
                permission, excludingRoleId: currentRoleId, cancellationToken);

            if (others > 0)
                continue;

            // Nadie fuera de su rol lo tiene: ¿hay otro usuario activo en el mismo rol?
            var sameRole = await _users.CountActiveByRoleAsync(currentRoleId, cancellationToken);
            if (sameRole > 1)
                continue;

            return Error.Conflict(
                $"El cambio dejaría el sistema sin ningún usuario activo con '{permission}'. " +
                "Asigna ese permiso a otro usuario activo antes de continuar.");
        }

        return null;
    }
}
