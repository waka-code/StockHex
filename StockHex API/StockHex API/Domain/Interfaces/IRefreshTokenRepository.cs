using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Busca por el hash del token; el valor en claro nunca se persiste.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Tokens vigentes del usuario, para revocar una sesión completa.</summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recorre la cadena de rotación desde <paramref name="tokenId"/> hacia adelante.
    /// Se usa al detectar la reutilización de un token ya rotado, para invalidar
    /// todos sus descendientes.
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> GetChainAsync(Guid tokenId, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    void Update(RefreshToken token);

    /// <summary>Borra tokens caducados o revocados hace tiempo; devuelve cuántos eliminó.</summary>
    Task<int> DeleteExpiredAsync(DateTime olderThan, CancellationToken cancellationToken = default);
}
