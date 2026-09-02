using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class SupplierRepository : ISupplierRepository
{
    private readonly ApplicationDbContext _context;

    public SupplierRepository(ApplicationDbContext context) => _context = context;

    public Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Suppliers.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<PagedResult<Supplier>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.Name.Contains(term) ||
                                     (s.Email != null && s.Email.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(s => s.Products)
            .OrderBy(s => s.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Supplier>(items, total, request.Page, request.PageSize);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Suppliers.AnyAsync(s => s.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        _context.Suppliers.AnyAsync(
            s => s.Name == name && (excludeId == null || s.Id != excludeId),
            cancellationToken);

    public Task<int> CountProductsAsync(Guid supplierId, CancellationToken cancellationToken = default) =>
        _context.Products.CountAsync(p => p.SupplierId == supplierId, cancellationToken);

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default) =>
        await _context.Suppliers.AddAsync(supplier, cancellationToken);

    public void Update(Supplier supplier) => _context.Suppliers.Update(supplier);

    public void Remove(Supplier supplier) => _context.Suppliers.Remove(supplier);
}
