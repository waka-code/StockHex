using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.InventoryMovementUseCases;

/// <summary>
/// Única vía por la que cambia el stock. Escribe el movimiento y ajusta
/// <see cref="Product.StockQuantity"/> en un solo SaveChanges, de forma que
/// el historial y el stock nunca puedan quedar desincronizados.
/// </summary>
public sealed class CreateMovement
{
    private readonly IInventoryMovementRepository _movements;
    private readonly IProductRepository _products;
    private readonly IClientRepository _clients;
    private readonly ISupplierRepository _suppliers;
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateMovement(
        IInventoryMovementRepository movements,
        IProductRepository products,
        IClientRepository clients,
        ISupplierRepository suppliers,
        IUserRepository users,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork)
    {
        _movements = movements;
        _products = products;
        _clients = clients;
        _suppliers = suppliers;
        _users = users;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public Task<Result<MovementResponse>> RunAsync(
        CreateMovementRequest request,
        CancellationToken cancellationToken = default) =>
        // Se reintenta porque varios movimientos del mismo producto compiten por su
        // token de concurrencia. El cuerpo relee el producto en cada intento, así que
        // el stock sobre el que decide es siempre el vigente.
        _unitOfWork.ExecuteWithConcurrencyRetryAsync(
            (_, ct) => RunCoreAsync(request, ct),
            cancellationToken);

    private async Task<Result<MovementResponse>> RunCoreAsync(
        CreateMovementRequest request,
        CancellationToken cancellationToken)
    {
        // El autor se toma del token, nunca del body, para que la auditoría no sea falsificable.
        var userId = _currentUser.Id;
        if (userId is null)
            return Result<MovementResponse>.Failure(
                Error.Unauthorized("Se requiere un usuario autenticado para registrar un movimiento."));

        if (!await _users.ExistsAsync(userId.Value, cancellationToken))
            return Result<MovementResponse>.Failure(
                Error.Unauthorized("El usuario del token ya no existe."));

        var product = await _products.GetByIdAsync(request.ProductId, includeRelations: true, cancellationToken);
        if (product is null)
            return Result<MovementResponse>.Failure(Error.NotFound("Producto", request.ProductId));

        if (!product.IsActive)
            return Result<MovementResponse>.Failure(
                Error.Conflict($"El producto '{product.Sku}' está desactivado y no admite movimientos."));

        if (request.ClientId.HasValue &&
            !await _clients.ExistsAsync(request.ClientId.Value, cancellationToken))
            return Result<MovementResponse>.Failure(Error.NotFound("Cliente", request.ClientId.Value));

        if (request.SupplierId.HasValue &&
            !await _suppliers.ExistsAsync(request.SupplierId.Value, cancellationToken))
            return Result<MovementResponse>.Failure(Error.NotFound("Proveedor", request.SupplierId.Value));

        var stockResult = CalculateStockAfter(product.StockQuantity, request.MovementType, request.Quantity, product.Sku);
        if (stockResult.IsFailure)
            return Result<MovementResponse>.Failure(stockResult.Error!);

        var movement = new InventoryMovement
        {
            ProductId = product.Id,
            MovementType = request.MovementType,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            StockBefore = product.StockQuantity,
            StockAfter = stockResult.Value,
            MovementDate = DateTime.UtcNow,
            UserId = userId.Value,
            ClientId = request.ClientId,
            // En una entrada sin contraparte explícita se hereda el proveedor del
            // producto: es el caso habitual (una compra) y evita perder el dato de
            // a quién se le compró. Nunca se sobrescribe un cliente indicado.
            SupplierId = request.SupplierId
                ?? (request.MovementType == MovementType.In && request.ClientId is null
                    ? product.SupplierId
                    : null),
            Comment = request.Comment?.Trim()
        };

        product.StockQuantity = movement.StockAfter;
        product.UpdatedAt = DateTime.UtcNow;

        await _movements.AddAsync(movement, cancellationToken);
        _products.Update(product);

        // Un único SaveChanges => una única transacción implícita: movimiento y stock, o nada.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var saved = await _movements.GetByIdAsync(movement.Id, cancellationToken);
        return (saved ?? movement).ToResponse();
    }

    /// <summary>Aplica la semántica de cada tipo de movimiento sobre el stock actual.</summary>
    private static Result<int> CalculateStockAfter(
        int currentStock,
        MovementType movementType,
        int quantity,
        string sku)
        => movementType switch
        {
            MovementType.In => currentStock + quantity,

            MovementType.Out when quantity > currentStock => Result<int>.Failure(
                Error.Conflict(
                    $"Stock insuficiente para '{sku}': disponible {currentStock}, solicitado {quantity}.")),

            MovementType.Out => currentStock - quantity,

            // En un ajuste, Quantity es el stock final que se quiere dejar registrado.
            MovementType.Adjustment => quantity,

            _ => Result<int>.Failure(
                Error.Validation($"Tipo de movimiento no soportado: {movementType}."))
        };
}
