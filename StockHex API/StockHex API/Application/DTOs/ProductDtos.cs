namespace StockHex_API.Application.DTOs;

/// <summary>
/// Alta de producto. No incluye stock: el inventario sólo se mueve a través de
/// <c>POST /api/inventory-movements</c>, así todo cambio queda auditado.
/// </summary>
public sealed record CreateProductRequest(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int MinimumStock,
    Guid CategoryId,
    Guid? SupplierId);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int MinimumStock,
    Guid CategoryId,
    Guid? SupplierId,
    bool IsActive);

public sealed record ProductResponse(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    int MinimumStock,
    bool IsLowStock,
    bool IsActive,
    Guid CategoryId,
    string? CategoryName,
    Guid? SupplierId,
    string? SupplierName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
