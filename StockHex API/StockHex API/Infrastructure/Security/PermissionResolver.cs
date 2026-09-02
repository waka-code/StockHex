using Microsoft.Extensions.Caching.Memory;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Infrastructure.Security;

public sealed class PermissionResolver : IPermissionResolver
{
    /// <summary>
    /// Ventana de la caché. Corta a propósito: acota cuánto puede tardar en surtir
    /// efecto un cambio de permisos si la invalidación explícita no llegara (por
    /// ejemplo, con varias instancias de la API y una caché por proceso).
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    public PermissionResolver(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    private static string KeyOf(Guid roleId) => $"permissions:{roleId}";

    public async Task<IReadOnlySet<string>> GetForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(KeyOf(roleId), out IReadOnlySet<string>? cached) && cached is not null)
            return cached;

        // Ámbito propio: este servicio es singleton y el repositorio es scoped.
        using var scope = _scopeFactory.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<IRoleRepository>();

        var permissions = await roles.GetPermissionsAsync(roleId, cancellationToken);
        IReadOnlySet<string> resolved = permissions.ToHashSet(StringComparer.Ordinal);

        _cache.Set(KeyOf(roleId), resolved, Ttl);

        return resolved;
    }

    public async Task<bool> HasPermissionAsync(
        Guid roleId,
        string permission,
        CancellationToken cancellationToken = default) =>
        (await GetForRoleAsync(roleId, cancellationToken)).Contains(permission);

    public void Invalidate(Guid roleId) => _cache.Remove(KeyOf(roleId));
}
