using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface IProductRepository
{
    /// <param name="includeRelations">Cuando es true trae Category y Supplier para poder mapear sus nombres.</param>
    Task<Product?> GetByIdAsync(Guid id, bool includeRelations = false, CancellationToken cancellationToken = default);

    Task<PagedResult<Product>> GetPagedAsync(ProductFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Productos activos en o por debajo de su mínimo, ordenados por déficit.</summary>
    Task<PagedResult<Product>> GetLowStockAsync(
        PageRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Cuenta los productos en stock bajo sin materializarlos.</summary>
    Task<int> CountLowStockAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsBySkuAsync(string sku, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Suma de Price * StockQuantity sobre los productos activos, calculada en la base de datos.</summary>
    Task<decimal> GetTotalStockValueAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(bool onlyActive = false, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    void Update(Product product);

    void Remove(Product product);
}
