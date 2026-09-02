using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly ApplicationDbContext _context;

    public RoleRepository(ApplicationDbContext context) => _context = context;

    public Task<Role?> GetByIdAsync(
        Guid id,
        bool includePermissions = true,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Roles.AsQueryable();

        if (includePermissions)
            query = query.Include(r => r.Permissions);

        return query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PagedResult<Role>> GetPagedAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.Name.Contains(term) ||
                                     (r.Description != null && r.Description.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(r => r.Permissions)
            // El rol de sistema primero: es el que se consulta más.
            .OrderByDescending(r => r.IsSystem)
            .ThenBy(r => r.Name)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Role>(items, total, request.Page, request.PageSize);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Roles.AnyAsync(r => r.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default) =>
        _context.Roles.AnyAsync(
            r => r.Name == name && (excludeId == null || r.Id != excludeId),
            cancellationToken);

    public Task<int> CountUsersAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(u => u.RoleId == roleId, cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid roleId,
        CancellationToken cancellationToken = default) =>
        await _context.RolePermissions
            .AsNoTracking()
            .Where(p => p.RoleId == roleId)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveUsersWithPermissionAsync(
        string permission,
        Guid? excludingRoleId = null,
        CancellationToken cancellationToken = default) =>
        _context.Users.CountAsync(
            u => u.IsActive
                 && (excludingRoleId == null || u.RoleId != excludingRoleId)
                 && u.Role!.Permissions.Any(p => p.Permission == permission),
            cancellationToken);

    public async Task AddAsync(Role role, CancellationToken cancellationToken = default) =>
        await _context.Roles.AddAsync(role, cancellationToken);

    /// <summary>
    /// Con la entidad ya rastreada no hace falta hacer nada: el tracker detecta los
    /// cambios solo. Llamar a Update() marcaría todo el grafo como Modified,
    /// incluidos los hijos recién añadidos, y EF intentaría un UPDATE de filas que
    /// todavía no existen. Sólo una entidad desprendida necesita adjuntarse.
    /// </summary>
    public void Update(Role role)
    {
        if (_context.Entry(role).State == EntityState.Detached)
            _context.Roles.Update(role);
    }

    public void Remove(Role role) => _context.Roles.Remove(role);

    public void ReplacePermissions(Role role, IEnumerable<string> permissions)
    {
        // Se normaliza contra el catálogo del código: nunca entra una clave inventada.
        var wanted = Permissions.Normalize(permissions).ToHashSet(StringComparer.Ordinal);

        var toRemove = role.Permissions.Where(p => !wanted.Contains(p.Permission)).ToList();
        foreach (var permission in toRemove)
        {
            role.Permissions.Remove(permission);
            _context.RolePermissions.Remove(permission);
        }

        var existing = role.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal);
        foreach (var key in wanted.Where(k => !existing.Contains(k)))
        {
            var permission = new RolePermission { RoleId = role.Id, Permission = key };

            role.Permissions.Add(permission);

            // Add explícito: la entidad ya trae un Id (lo asigna el inicializador de
            // la propiedad), y al añadirla a un grafo rastreado EF deduce del PK no
            // vacío que la fila ya existe y la marca Modified en lugar de Added,
            // provocando un UPDATE de algo que nunca se insertó.
            _context.RolePermissions.Add(permission);
        }
    }
}
