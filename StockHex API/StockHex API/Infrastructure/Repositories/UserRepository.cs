using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
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
        // tiene actividad usa CountMovementsAsync. El rol sí se trae: hace falta para
        // mapear la respuesta y para los guardias de permisos.
        _context.Users
            .Include(u => u.Role)
                .ThenInclude(r => r!.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<PagedResult<User>> GetPagedAsync(
        UserFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(u => u.Name.Contains(term) || u.Email.Contains(term));
        }

        if (filter.RoleId.HasValue)
            query = query.Where(u => u.RoleId == filter.RoleId.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(u => u.IsActive == filter.IsActive.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(u => u.Role)
            .OrderBy(u => u.Name)
            .Skip(filter.Skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<User>(items, total, filter.Page, filter.PageSize);
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

    public Task<int> CountActiveByRoleAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(u => u.RoleId == roleId && u.IsActive, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    /// <summary>
    /// Con la entidad ya rastreada no hace falta hacer nada: el tracker detecta los
    /// cambios solo. Llamar a Update() marcaría todo el grafo como Modified,
    /// incluidos los hijos recién añadidos, y EF intentaría un UPDATE de filas que
    /// todavía no existen. Sólo una entidad desprendida necesita adjuntarse.
    /// </summary>
    public void Update(User user)
    {
        if (_context.Entry(user).State == EntityState.Detached)
            _context.Users.Update(user);
    }

    public void Remove(User user) => _context.Users.Remove(user);
}
