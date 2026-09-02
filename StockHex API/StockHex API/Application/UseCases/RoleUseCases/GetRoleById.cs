using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.RoleUseCases;

public sealed class GetRoleById
{
    private readonly IRoleRepository _roles;

    public GetRoleById(IRoleRepository roles) => _roles = roles;

    public async Task<Result<RoleResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var role = await _roles.GetByIdAsync(id, includePermissions: true, cancellationToken);
        if (role is null)
            return Result<RoleResponse>.Failure(Error.NotFound("Rol", id));

        var users = await _roles.CountUsersAsync(id, cancellationToken);
        return role.ToResponse(users);
    }
}
