using StockHex_API.Domain.Enums;

namespace StockHex_API.Application.DTOs;

/// <summary>
/// Registra un movimiento. El usuario no se envía en el body: se toma del token JWT
/// para que la auditoría no sea falsificable.
///
/// <c>ClientId</c> y <c>SupplierId</c> son la contraparte del movimiento y son
/// mutuamente excluyentes: una compra o devolución a proveedor lleva proveedor,
/// una venta o devolución de cliente lleva cliente.
/// </summary>
public sealed record CreateMovementRequest(
    Guid ProductId,
    MovementType MovementType,
    int Quantity,
    decimal? UnitPrice,
    Guid? ClientId,
    Guid? SupplierId,
    string? Comment);

/// <summary>
/// Corrige un movimiento equivocado. No lo edita ni lo borra: registra el
/// movimiento inverso, de modo que el error y su corrección quedan en el historial.
/// </summary>
public sealed record ReverseMovementRequest(string? Comment);

public sealed record MovementResponse(
    Guid Id,
    MovementType MovementType,
    Guid ProductId,
    string? ProductName,
    string? ProductSku,
    int Quantity,
    decimal? UnitPrice,
    int StockBefore,
    int StockAfter,
    DateTime MovementDate,
    Guid UserId,
    string? UserName,
    Guid? ClientId,
    string? ClientName,
    Guid? SupplierId,
    string? SupplierName,
    Guid? ReversalOfMovementId,
    string? Comment);
