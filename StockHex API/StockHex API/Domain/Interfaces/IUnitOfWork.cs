namespace StockHex_API.Domain.Interfaces;

/// <summary>
/// Punto único de confirmación. Los repositorios sólo marcan cambios; la use case
/// decide cuándo persistirlos, de modo que varios cambios se guarden atómicamente.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ejecuta <paramref name="operation"/> y la reintenta desde cero si otra
    /// transacción modificó entre medias un registro con token de concurrencia.
    /// Entre intentos se descartan los cambios pendientes, así que la operación
    /// debe releer lo que necesite: se la invoca varias veces.
    /// </summary>
    /// <param name="attempt">Número de intento, empezando en 1.</param>
    Task<T> ExecuteWithConcurrencyRetryAsync<T>(
        Func<int, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
