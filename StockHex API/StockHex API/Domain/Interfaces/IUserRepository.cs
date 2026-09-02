using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<PagedResult<User>> GetPagedAsync(UserFilter filter, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Comprobación en base de datos; no carga la tabla en memoria.</summary>
    Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Usuarios activos con el rol indicado. Sustituye al conteo por enum.</summary>
    Task<int> CountActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Cuenta en base de datos, sin materializar la colección de movimientos.</summary>
    Task<int> CountMovementsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Update(User user);

    void Remove(User user);
}
