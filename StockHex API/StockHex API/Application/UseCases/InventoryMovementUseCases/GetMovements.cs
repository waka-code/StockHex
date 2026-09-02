using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.InventoryMovementUseCases;

public sealed class GetMovements
{
    private readonly IInventoryMovementRepository _movements;

    public GetMovements(IInventoryMovementRepository movements) => _movements = movements;

    public async Task<Result<PagedResponse<MovementResponse>>> RunAsync(
        MovementFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
            return Result<PagedResponse<MovementResponse>>.Failure(
                Error.Validation("'From' no puede ser posterior a 'To'."));

        var page = await _movements.GetPagedAsync(filter, cancellationToken);
        return PagedResponse<MovementResponse>.From(page, m => m.ToResponse());
    }
}
