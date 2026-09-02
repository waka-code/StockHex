using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Tests.Common;

/// <summary>
/// Resuelve los permisos leyendo el contexto de prueba. El resolver real usa un
/// ámbito propio y una caché, que aquí sobran: el test quiere ver el estado
/// actual, no uno cacheado.
/// </summary>
internal sealed class StubPermissionResolver : IPermissionResolver
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Permisos servidos sin tocar el contexto. Los usa el rol de quien llama: darle
    /// una fila real cambiaría los recuentos de <c>Roles</c> que varios tests afirman.
    /// </summary>
    private readonly Dictionary<Guid, IReadOnlySet<string>> _overrides = [];

    public StubPermissionResolver(ApplicationDbContext context) => _context = context;

    public int InvalidateCalls { get; private set; }

    /// <summary>Fija los permisos de un rol que no existe como fila.</summary>
    public StubPermissionResolver With(Guid roleId, IEnumerable<string> permissions)
    {
        _overrides[roleId] = permissions.ToHashSet(StringComparer.Ordinal);
        return this;
    }

    public async Task<IReadOnlySet<string>> GetForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (_overrides.TryGetValue(roleId, out var fixedSet))
            return fixedSet;

        var keys = await _context.RolePermissions
            .Where(p => p.RoleId == roleId)
            .Select(p => p.Permission)
            .ToListAsync(cancellationToken);

        return keys.ToHashSet(StringComparer.Ordinal);
    }

    public async Task<bool> HasPermissionAsync(
        Guid roleId,
        string permission,
        CancellationToken cancellationToken = default) =>
        (await GetForRoleAsync(roleId, cancellationToken)).Contains(permission);

    public void Invalidate(Guid roleId) => InvalidateCalls++;
}

/// <summary>
/// Lee el estado de la cuenta del contexto de prueba, sin la caché del resolver
/// real, y lleva la cuenta de las invalidaciones para poder afirmarlas.
/// </summary>
internal sealed class StubActiveUserResolver : IActiveUserResolver
{
    private readonly ApplicationDbContext _context;

    public StubActiveUserResolver(ApplicationDbContext context) => _context = context;

    public int InvalidateCalls { get; private set; }

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _context.Users
            .Where(u => u.Id == userId)
            .Select(u => u.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

    public void Invalidate(Guid userId) => InvalidateCalls++;
}

/// <summary>Devuelve un rol fijo como destino del auto-registro.</summary>
internal sealed class StubDefaultRoleProvider : IDefaultRoleProvider
{
    private readonly Guid? _roleId;

    public StubDefaultRoleProvider(Guid? roleId) => _roleId = roleId;

    public Task<Guid?> GetRegistrationRoleIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_roleId);
}
