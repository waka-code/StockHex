using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;

    public ProductRepository(ApplicationDbContext context) => _context = context;

    public Task<Product?> GetByIdAsync(
        Guid id,
        bool includeRelations = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsQueryable();

        if (includeRelations)
            query = query.Include(p => p.Category).Include(p => p.Supplier);

        return query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Product>> GetPagedAsync(
        ProductFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) ||
                                     p.Sku.Contains(term) ||
                                     (p.Description != null && p.Description.Contains(term)));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (filter.SupplierId.HasValue)
            query = query.Where(p => p.SupplierId == filter.SupplierId.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        // IsLowStock es una propiedad calculada en C# y no se traduce a SQL:
        // se replica aquí como comparación de columnas para que filtre en la base.
        if (filter.LowStockOnly)
            query = query.Where(p => p.StockQuantity <= p.MinimumStock);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<Product>> GetLowStockAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.StockQuantity <= p.MinimumStock);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(p => p.Category)
            // Primero lo más urgente: el mayor déficit respecto del mínimo. Se ordena
            // en la base para que la página 1 sean de verdad los casos más críticos,
            // no los primeros por nombre.
            .OrderByDescending(p => p.MinimumStock - p.StockQuantity)
            .ThenBy(p => p.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Product>(items, total, request.Page, request.PageSize);
    }

    public Task<int> CountLowStockAsync(CancellationToken cancellationToken = default) =>
        _context.Products.CountAsync(
            p => p.IsActive && p.StockQuantity <= p.MinimumStock,
            cancellationToken);

    public Task<bool> ExistsBySkuAsync(
        string sku,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        _context.Products.AnyAsync(
            p => p.Sku == sku && (excludeId == null || p.Id != excludeId),
            cancellationToken);

    public async Task<decimal> GetTotalStockValueAsync(CancellationToken cancellationToken = default) =>
        await _context.Products
            .Where(p => p.IsActive)
            // SumAsync sobre un conjunto vacío devuelve null en SQL, de ahí el cast a decimal?.
            .SumAsync(p => (decimal?)(p.Price * p.StockQuantity), cancellationToken) ?? 0m;

    public Task<int> CountAsync(bool onlyActive = false, CancellationToken cancellationToken = default) =>
        onlyActive
            ? _context.Products.CountAsync(p => p.IsActive, cancellationToken)
            : _context.Products.CountAsync(cancellationToken);

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default) =>
        await _context.Products.AddAsync(product, cancellationToken);

    public void Update(Product product) => _context.Products.Update(product);

    public void Remove(Product product) => _context.Products.Remove(product);
}
