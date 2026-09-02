using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.InventoryMovementUseCases;

public sealed class GetMovementById
{
    private readonly IInventoryMovementRepository _movements;

    public GetMovementById(IInventoryMovementRepository movements) => _movements = movements;

    public async Task<Result<MovementResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var movement = await _movements.GetByIdAsync(id, cancellationToken);

        return movement is null
            ? Result<MovementResponse>.Failure(Error.NotFound("Movimiento", id))
            : movement.ToResponse();
    }
}
