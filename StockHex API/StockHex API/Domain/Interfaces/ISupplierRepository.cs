using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<Supplier>> GetPagedAsync(PageRequest request, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<int> CountProductsAsync(Guid supplierId, CancellationToken cancellationToken = default);

    Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default);

    void Update(Supplier supplier);

    void Remove(Supplier supplier);
}
