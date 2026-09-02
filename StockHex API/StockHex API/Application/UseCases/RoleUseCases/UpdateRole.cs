using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.RoleUseCases;

public sealed class UpdateRole
{
    private readonly IRoleRepository _roles;
    private readonly IPermissionResolver _permissions;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRole(IRoleRepository roles, IPermissionResolver permissions, IUnitOfWork unitOfWork)
    {
        _roles = roles;
        _permissions = permissions;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RoleResponse>> RunAsync(
        Guid id,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var role = await _roles.GetByIdAsync(id, includePermissions: true, cancellationToken);
        if (role is null)
            return Result<RoleResponse>.Failure(Error.NotFound("Rol", id));

        var name = request.Name.Trim();

        if (await _roles.ExistsByNameAsync(name, id, cancellationToken))
            return Result<RoleResponse>.Failure(Error.Conflict($"Ya existe otro rol llamado '{name}'."));

        var unknown = Permissions.Unknown(request.Permissions);
        if (unknown.Count > 0)
            return Result<RoleResponse>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Permissions)] =
                    [$"Permisos desconocidos: {string.Join(", ", unknown)}."],
            }));

        var wanted = Permissions.Normalize(request.Permissions).ToHashSet(StringComparer.Ordinal);

        // Un rol de sistema no se puede dejar sin los permisos críticos: es el
        // último recurso para volver a administrar el sistema.
        if (role.IsSystem)
        {
            var missing = Permissions.Critical.Where(p => !wanted.Contains(p)).ToList();
            if (missing.Count > 0)
                return Result<RoleResponse>.Failure(Error.Conflict(
                    $"El rol de sistema '{role.Name}' no puede quedarse sin {string.Join(" ni ", missing)}."));
        }
        else
        {
            // Para un rol normal, quitarle un permiso crítico sólo se bloquea si
            // deja el sistema sin ningún otro usuario activo que lo conserve.
            var losing = Permissions.Critical
                .Where(p => role.Grants(p) && !wanted.Contains(p))
                .ToList();

            foreach (var permission in losing)
            {
                var elsewhere = await _roles.CountActiveUsersWithPermissionAsync(
                    permission, excludingRoleId: role.Id, cancellationToken);

                if (elsewhere == 0)
                    return Result<RoleResponse>.Failure(Error.Conflict(
                        $"Quitar '{permission}' a este rol dejaría el sistema sin nadie que pueda " +
                        "administrar roles y usuarios. Concédelo antes a otro rol activo."));
            }
        }

        role.Name = name;
        role.Description = request.Description?.Trim();
        role.UpdatedAt = DateTime.UtcNow;

        _roles.ReplacePermissions(role, wanted);
        _roles.Update(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Invalidación explícita: el cambio surte efecto de inmediato en lugar de
        // esperar a que caduque la entrada de la caché.
        _permissions.Invalidate(role.Id);

        var users = await _roles.CountUsersAsync(id, cancellationToken);
        return role.ToResponse(users);
    }
}
