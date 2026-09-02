using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class ClientRepository : IClientRepository
{
    private readonly ApplicationDbContext _context;

    public ClientRepository(ApplicationDbContext context) => _context = context;

    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<PagedResult<Client>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(term) ||
                                     (c.Email != null && c.Email.Contains(term)) ||
                                     (c.PhoneNumber != null && c.PhoneNumber.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Client>(items, total, request.Page, request.PageSize);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Clients.AnyAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        _context.Clients.AnyAsync(
            c => c.Email == email && (excludeId == null || c.Id != excludeId),
            cancellationToken);

    public Task<int> CountMovementsAsync(Guid clientId, CancellationToken cancellationToken = default) =>
        _context.InventoryMovements.CountAsync(m => m.ClientId == clientId, cancellationToken);

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default) =>
        await _context.Clients.AddAsync(client, cancellationToken);

    public void Update(Client client) => _context.Clients.Update(client);

    public void Remove(Client client) => _context.Clients.Remove(client);
}
