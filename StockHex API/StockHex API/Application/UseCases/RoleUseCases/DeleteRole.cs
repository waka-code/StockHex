using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.RoleUseCases;

public sealed class DeleteRole
{
    private readonly IRoleRepository _roles;
    private readonly IPermissionResolver _permissions;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRole(IRoleRepository roles, IPermissionResolver permissions, IUnitOfWork unitOfWork)
    {
        _roles = roles;
        _permissions = permissions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await _roles.GetByIdAsync(id, includePermissions: true, cancellationToken);
        if (role is null)
            return Result.Failure(Error.NotFound("Rol", id));

        if (role.IsSystem)
            return Result.Failure(Error.Conflict(
                $"El rol de sistema '{role.Name}' no se puede eliminar."));

        // Los usuarios apuntan al rol con FK restrictiva: hay que reasignarlos primero.
        var users = await _roles.CountUsersAsync(id, cancellationToken);
        if (users > 0)
            return Result.Failure(Error.Conflict(
                $"No se puede eliminar el rol porque tiene {users} usuario(s) asignado(s). " +
                "Reasígnalos a otro rol primero."));

        _roles.Remove(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _permissions.Invalidate(id);

        return Result.Success();
    }
}
