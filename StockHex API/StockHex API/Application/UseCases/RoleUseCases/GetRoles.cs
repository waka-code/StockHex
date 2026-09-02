using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.RoleUseCases;

public sealed class GetRoles
{
    private readonly IRoleRepository _roles;

    public GetRoles(IRoleRepository roles) => _roles = roles;

    public async Task<Result<PagedResponse<RoleResponse>>> RunAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _roles.GetPagedAsync(request, cancellationToken);

        // El conteo de usuarios se resuelve por rol en la base; son pocas filas
        // (una consulta por rol de la página) y evita traer los usuarios.
        var counts = new Dictionary<Guid, int>();
        foreach (var role in page.Items)
            counts[role.Id] = await _roles.CountUsersAsync(role.Id, cancellationToken);

        return PagedResponse<RoleResponse>.From(page, r => r.ToResponse(counts[r.Id]));
    }
}
