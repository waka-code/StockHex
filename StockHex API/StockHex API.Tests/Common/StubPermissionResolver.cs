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

    public StubPermissionResolver(ApplicationDbContext context) => _context = context;

    public int InvalidateCalls { get; private set; }

    public async Task<IReadOnlySet<string>> GetForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
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

/// <summary>Devuelve un rol fijo como destino del auto-registro.</summary>
internal sealed class StubDefaultRoleProvider : IDefaultRoleProvider
{
    private readonly Guid? _roleId;

    public StubDefaultRoleProvider(Guid? roleId) => _roleId = roleId;

    public Task<Guid?> GetRegistrationRoleIdAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_roleId);
}
