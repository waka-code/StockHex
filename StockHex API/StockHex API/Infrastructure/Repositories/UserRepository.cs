using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context) => _context = context;

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        // Sin Include de Movements: un usuario con años de actividad traería miles de
        // filas para responder un perfil que no las muestra. Quien necesite saber si
        // tiene actividad usa CountMovementsAsync.
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<PagedResult<User>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(u => u.Name.Contains(term) || u.Email.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(u => u.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, total, request.Page, request.PageSize);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(u => u.Id == id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(
        string email,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(
            u => u.Email == email && (excludeId == null || u.Id != excludeId),
            cancellationToken);

    public Task<int> CountMovementsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.InventoryMovements.CountAsync(m => m.UserId == userId, cancellationToken);

    public Task<int> CountByRoleAsync(UserRole role, CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(u => u.Role == role && u.IsActive, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public void Update(User user) => _context.Users.Update(user);

    public void Remove(User user) => _context.Users.Remove(user);
}
