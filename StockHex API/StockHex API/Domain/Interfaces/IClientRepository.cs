using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Client>> GetPagedAsync(PageRequest request, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<int> CountMovementsAsync(Guid clientId, CancellationToken cancellationToken = default);

    Task AddAsync(Client client, CancellationToken cancellationToken = default);

    void Update(Client client);

    void Remove(Client client);
}
