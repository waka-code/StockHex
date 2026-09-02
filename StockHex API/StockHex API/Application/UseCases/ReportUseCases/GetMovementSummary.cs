using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ReportUseCases;

/// <summary>Actividad de inventario agregada por tipo de movimiento en un rango de fechas.</summary>
public sealed class GetMovementSummary
{
    private readonly IInventoryMovementRepository _movements;

    public GetMovementSummary(IInventoryMovementRepository movements) => _movements = movements;

    public async Task<Result<MovementSummaryResponse>> RunAsync(
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var end = to ?? DateTime.UtcNow;
        // Sin rango explícito, el reporte cubre los últimos 30 días.
        var start = from ?? end.AddDays(-30);

        if (start > end)
            return Result<MovementSummaryResponse>.Failure(
                Error.Validation("'from' no puede ser posterior a 'to'."));

        var summary = await _movements.GetSummaryAsync(start, end, cancellationToken);

        var lines = summary
            .Select(kv => new MovementSummaryLine(kv.Key, kv.Value.Movements, kv.Value.Units))
            .OrderBy(l => l.MovementType)
            .ToList();

        return new MovementSummaryResponse(start, end, lines, DateTime.UtcNow);
    }
}
