using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.InventoryMovementUseCases;

/// <summary>
/// Corrige un movimiento equivocado registrando su inverso. El movimiento original
/// nunca se edita ni se borra: el historial es un registro contable, así que el
/// error y su corrección conviven en él.
/// </summary>
public sealed class ReverseMovement
{
    private readonly IInventoryMovementRepository _movements;
    private readonly IProductRepository _products;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ReverseMovement(
        IInventoryMovementRepository movements,
        IProductRepository products,
        IUserRepository users,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _movements = movements;
        _products = products;
        _users = users;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<MovementResponse>> RunAsync(
        Guid movementId,
        ReverseMovementRequest request,
        CancellationToken cancellationToken = default) =>
        _unitOfWork.ExecuteWithConcurrencyRetryAsync(
            (_, ct) => RunCoreAsync(movementId, request, ct),
            cancellationToken);

    private async Task<Result<MovementResponse>> RunCoreAsync(
        Guid movementId,
        ReverseMovementRequest request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.Id;
        if (userId is null)
            return Result<MovementResponse>.Failure(
                Error.Unauthorized("Se requiere un usuario autenticado para revertir un movimiento."));

        if (!await _users.ExistsAsync(userId.Value, cancellationToken))
            return Result<MovementResponse>.Failure(
                Error.Unauthorized("El usuario del token ya no existe."));

        var original = await _movements.GetByIdAsync(movementId, cancellationToken);
        if (original is null)
            return Result<MovementResponse>.Failure(Error.NotFound("Movimiento", movementId));

        if (original.IsReversal)
            return Result<MovementResponse>.Failure(Error.Conflict(
                "No se puede revertir una reversión. Si la corrección fue equivocada, " +
                "registra un movimiento nuevo."));

        if (await _movements.HasReversalAsync(movementId, cancellationToken))
            return Result<MovementResponse>.Failure(Error.Conflict(
                "Este movimiento ya fue revertido."));

        // Se invierte la variación neta, no la cantidad: así funciona igual para
        // entradas, salidas y ajustes, sin importar cuántos movimientos hubo después.
        var delta = original.Delta;

        if (delta == 0)
            return Result<MovementResponse>.Failure(Error.Conflict(
                "El movimiento no alteró el stock, no hay nada que revertir."));

        // Se permite revertir sobre un producto desactivado: es una corrección de
        // historial, no actividad nueva, y si no se pudiera el error quedaría fijo.
        var product = await _products.GetByIdAsync(original.ProductId, includeRelations: false, cancellationToken);
        if (product is null)
            return Result<MovementResponse>.Failure(Error.NotFound("Producto", original.ProductId));

        var stockAfter = product.StockQuantity - delta;

        if (stockAfter < 0)
            return Result<MovementResponse>.Failure(Error.Conflict(
                $"Revertir el movimiento dejaría el stock de '{product.Sku}' en {stockAfter}. " +
                $"Stock actual {product.StockQuantity}, se requieren {delta} unidades."));

        var reversal = new InventoryMovement
        {
            ProductId = product.Id,
            // El tipo refleja el efecto de la corrección: si el original sumó, ésta resta.
            MovementType = delta > 0 ? MovementType.Out : MovementType.In,
            Quantity = Math.Abs(delta),
            UnitPrice = original.UnitPrice,
            StockBefore = product.StockQuantity,
            StockAfter = stockAfter,
            MovementDate = DateTime.UtcNow,
            UserId = userId.Value,
            // Se conservan cliente y proveedor del original para que la corrección
            // aparezca en los mismos filtros que el movimiento que corrige.
            ClientId = original.ClientId,
            SupplierId = original.SupplierId,
            ReversalOfMovementId = original.Id,
            Comment = BuildComment(original, request.Comment)
        };

        product.StockQuantity = stockAfter;
        product.UpdatedAt = DateTime.UtcNow;

        await _movements.AddAsync(reversal, cancellationToken);
        _products.Update(product);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _movements.GetByIdAsync(reversal.Id, cancellationToken);
        return (saved ?? reversal).ToResponse();
    }

    private static string BuildComment(InventoryMovement original, string? userComment)
    {
        var prefix = $"Reversión de {original.MovementType} de {original.Quantity} " +
                     $"del {original.MovementDate:yyyy-MM-dd HH:mm} UTC";

        return string.IsNullOrWhiteSpace(userComment)
            ? prefix
            : $"{prefix}. {userComment.Trim()}";
    }
}
