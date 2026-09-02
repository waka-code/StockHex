using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.RoleUseCases;

public sealed class CreateRole
{
    private readonly IRoleRepository _roles;
    private readonly IPermissionResolver _permissions;
    private readonly PermissionEscalationGuard _escalation;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRole(
        IRoleRepository roles,
        IPermissionResolver permissions,
        PermissionEscalationGuard escalation,
        IUnitOfWork unitOfWork)
    {
        _roles = roles;
        _permissions = permissions;
        _escalation = escalation;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RoleResponse>> RunAsync(
        CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _roles.ExistsByNameAsync(name, null, cancellationToken))
            return Result<RoleResponse>.Failure(Error.Conflict($"Ya existe un rol llamado '{name}'."));

        // Una clave que no está en el catálogo del código no protege nada: se
        // rechaza en lugar de guardarla y dar la impresión de que concede algo.
        var unknown = Permissions.Unknown(request.Permissions);
        if (unknown.Count > 0)
            return Result<RoleResponse>.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.Permissions)] =
                    [$"Permisos desconocidos: {string.Join(", ", unknown)}."],
            }));

        var wanted = Permissions.Normalize(request.Permissions);

        // Un rol nuevo se crea desde cero, así que todo lo que conceda es alta.
        var escalation = await _escalation.RejectEscalationAsync(wanted, [], cancellationToken);
        if (escalation is not null)
            return Result<RoleResponse>.Failure(escalation);

        var role = new Role
        {
            Name = name,
            Description = request.Description?.Trim(),
            IsSystem = false,
        };

        foreach (var key in wanted)
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = key });

        await _roles.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _permissions.Invalidate(role.Id);

        return role.ToResponse(userCount: 0);
    }
}
