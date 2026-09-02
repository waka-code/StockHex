using Microsoft.Extensions.Options;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Infrastructure.BackgroundServices;

public sealed class RefreshTokenCleanupOptions
{
    public const string SectionName = "RefreshTokenCleanup";

    public bool Enabled { get; set; } = true;

    /// <summary>Cada cuánto se ejecuta la purga.</summary>
    public int IntervalHours { get; set; } = 24;

    /// <summary>
    /// Margen antes de borrar. Se conservan los tokens caducados o revocados
    /// recientemente para que sigan siendo visibles al investigar un incidente.
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}

/// <summary>
/// Borra periódicamente los refresh tokens caducados o revocados hace tiempo. Sin
/// esto la tabla crece sin límite: cada login y cada rotación añaden una fila.
/// </summary>
public sealed class RefreshTokenCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RefreshTokenCleanupOptions _options;
    private readonly ILogger<RefreshTokenCleanupService> _logger;

    public RefreshTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<RefreshTokenCleanupOptions> options,
        ILogger<RefreshTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("La purga de refresh tokens está desactivada.");
            return;
        }

        var interval = TimeSpan.FromHours(Math.Max(1, _options.IntervalHours));

        while (!stoppingToken.IsCancellationRequested)
        {
            await PurgeAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Apagado normal de la aplicación.
                return;
            }
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Ámbito propio: este servicio es singleton y el repositorio es scoped.
            using var scope = _scopeFactory.CreateScope();

            var tokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var cutoff = DateTime.UtcNow.AddDays(-Math.Max(0, _options.RetentionDays));
            var removed = await tokens.DeleteExpiredAsync(cutoff, cancellationToken);

            if (removed > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Purgados {Count} refresh tokens anteriores a {Cutoff}.",
                    removed, cutoff);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo de purga no debe tumbar la aplicación; se reintenta al siguiente ciclo.
            _logger.LogError(ex, "Falló la purga de refresh tokens; se reintentará.");
        }
    }
}
