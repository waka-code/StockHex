using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StockHex_API.Application.Abstractions;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Security;

public sealed class ActiveUserResolver : IActiveUserResolver
{
    /// <summary>
    /// La misma ventana que <see cref="PermissionResolver"/>, y por el mismo motivo:
    /// acota cuánto puede tardar en surtir efecto una baja si la invalidación
    /// explícita no llegara — con varias instancias de la API, la caché es por proceso.
    /// </summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    public ActiveUserResolver(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    private static string KeyOf(Guid userId) => $"user-active:{userId}";

    public async Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(KeyOf(userId), out bool cached))
            return cached;

        // Ámbito propio: este servicio es singleton y el contexto es scoped.
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Sólo la columna: es el camino caliente de cada petición autenticada.
        var active = await context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        // FirstOrDefault sobre bool devuelve false cuando el usuario no existe, que
        // es justo la respuesta correcta: un token de una cuenta borrada no vale.
        _cache.Set(KeyOf(userId), active, Ttl);

        return active;
    }

    public void Invalidate(Guid userId) => _cache.Remove(KeyOf(userId));
}
