using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class InventoryMovementRepository : IInventoryMovementRepository
{
    private readonly ApplicationDbContext _context;

    public InventoryMovementRepository(ApplicationDbContext context) => _context = context;

    public Task<InventoryMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.User)
            .Include(m => m.Client)
            .Include(m => m.Supplier)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<PagedResult<InventoryMovement>> GetPagedAsync(
        MovementFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.InventoryMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.User)
            .Include(m => m.Client)
            .Include(m => m.Supplier)
            .AsQueryable();

        if (filter.ProductId.HasValue)
            query = query.Where(m => m.ProductId == filter.ProductId.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(m => m.ClientId == filter.ClientId.Value);

        if (filter.SupplierId.HasValue)
            query = query.Where(m => m.SupplierId == filter.SupplierId.Value);

        if (filter.UserId.HasValue)
            query = query.Where(m => m.UserId == filter.UserId.Value);

        if (filter.MovementType.HasValue)
            query = query.Where(m => m.MovementType == filter.MovementType.Value);

        if (filter.From.HasValue)
            query = query.Where(m => m.MovementDate >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(m => m.MovementDate <= filter.To.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(m => (m.Comment != null && m.Comment.Contains(term)) ||
                                     (m.Product != null &&
                                      (m.Product.Name.Contains(term) || m.Product.Sku.Contains(term))));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            // Más reciente primero: es el orden que espera un historial.
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<InventoryMovement>(items, total, filter.Page, filter.PageSize);
    }

    public Task<int> CountByProductAsync(Guid productId, CancellationToken cancellationToken = default) =>
        _context.InventoryMovements.CountAsync(m => m.ProductId == productId, cancellationToken);

    public Task<bool> HasReversalAsync(Guid movementId, CancellationToken cancellationToken = default) =>
        _context.InventoryMovements.AnyAsync(
            m => m.ReversalOfMovementId == movementId,
            cancellationToken);

    public async Task<IReadOnlyDictionary<MovementType, (int Movements, int Units)>> GetSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        // La agregación la hace SQL Server; sólo viaja una fila por tipo de movimiento.
        var rows = await _context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.MovementDate >= from && m.MovementDate <= to)
            .GroupBy(m => m.MovementType)
            .Select(g => new
            {
                MovementType = g.Key,
                Movements = g.Count(),
                Units = g.Sum(m => m.Quantity)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.MovementType, r => (r.Movements, r.Units));
    }

    public async Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken = default) =>
        await _context.InventoryMovements.AddAsync(movement, cancellationToken);
}
