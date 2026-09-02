using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface IRoleRepository
{
    /// <param name="includePermissions">Trae la colección de permisos concedidos.</param>
    Task<Role?> GetByIdAsync(
        Guid id,
        bool includePermissions = true,
        CancellationToken cancellationToken = default);

    Task<PagedResult<Role>> GetPagedAsync(PageRequest request, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Los permisos que concede el rol. Es la consulta del camino caliente de autorización.</summary>
    Task<IReadOnlyList<string>> GetPermissionsAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuántos usuarios activos conservarían el permiso indicado si se excluye a un
    /// rol del recuento. Sirve para no dejar el sistema sin quien lo administre.
    /// </summary>
    Task<int> CountActiveUsersWithPermissionAsync(
        string permission,
        Guid? excludingRoleId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Role role, CancellationToken cancellationToken = default);

    void Update(Role role);

    void Remove(Role role);

    /// <summary>Reemplaza por completo los permisos del rol.</summary>
    void ReplacePermissions(Role role, IEnumerable<string> permissions);
}
