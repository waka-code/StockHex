using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces;

public interface IInventoryMovementRepository
{
    Task<InventoryMovement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<InventoryMovement>> GetPagedAsync(MovementFilter filter, CancellationToken cancellationToken = default);

    Task<int> CountByProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>True si ya existe un movimiento que revierte a <paramref name="movementId"/>.</summary>
    Task<bool> HasReversalAsync(Guid movementId, CancellationToken cancellationToken = default);

    /// <summary>Totales agregados por tipo de movimiento en un rango de fechas, para el reporte de actividad.</summary>
    Task<IReadOnlyDictionary<Enums.MovementType, (int Movements, int Units)>> GetSummaryAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task AddAsync(InventoryMovement movement, CancellationToken cancellationToken = default);
}
