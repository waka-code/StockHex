using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context;

    public RefreshTokenRepository(ApplicationDbContext context) => _context = context;

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetChainAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        // La cadena se recorre en bucle en lugar de con un CTE recursivo: son unos
        // pocos saltos (uno por rotación de la sesión) y así el repositorio no
        // depende de SQL específico del motor.
        var chain = new List<RefreshToken>();
        var currentId = (Guid?)tokenId;
        var visited = new HashSet<Guid>();

        while (currentId.HasValue && visited.Add(currentId.Value))
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.Id == currentId.Value, cancellationToken);

            if (token is null)
                break;

            chain.Add(token);
            currentId = token.ReplacedByTokenId;
        }

        return chain;
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens.AddAsync(token, cancellationToken);

    /// <summary>
    /// Con la entidad ya rastreada no hace falta hacer nada: el tracker detecta los
    /// cambios solo. Llamar a Update() marcaría todo el grafo como Modified,
    /// incluidos los hijos recién añadidos, y EF intentaría un UPDATE de filas que
    /// todavía no existen. Sólo una entidad desprendida necesita adjuntarse.
    /// </summary>
    public void Update(RefreshToken token)
    {
        if (_context.Entry(token).State == EntityState.Detached)
            _context.RefreshTokens.Update(token);
    }

    public async Task<int> DeleteExpiredAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        var stale = await _context.RefreshTokens
            .Where(t => t.ExpiresAt < olderThan ||
                        (t.RevokedAt != null && t.RevokedAt < olderThan))
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
            return 0;

        _context.RefreshTokens.RemoveRange(stale);
        return stale.Count;
    }
}
