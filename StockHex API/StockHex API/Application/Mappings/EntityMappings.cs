using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Application.Mappings;

/// <summary>
/// Mapeo explícito entidad -> DTO. Se hace a mano en lugar de con AutoMapper:
/// es verificado por el compilador (un campo nuevo rompe el build en vez de
/// llegar en null a producción) y evita una dependencia extra.
/// </summary>
public static class EntityMappings
{
    public static CategoryResponse ToResponse(this Category category, int productCount = 0) =>
        new(
            category.Id,
            category.Name,
            category.Description,
            productCount,
            category.CreatedAt,
            category.UpdatedAt);

    public static SupplierResponse ToResponse(this Supplier supplier, int productCount = 0) =>
        new(
            supplier.Id,
            supplier.Name,
            supplier.Description,
            supplier.PhoneNumber,
            supplier.Email,
            productCount,
            supplier.CreatedAt,
            supplier.UpdatedAt);

    public static ClientResponse ToResponse(this Client client) =>
        new(
            client.Id,
            client.Name,
            client.Address,
            client.PhoneNumber,
            client.Email,
            client.CreatedAt,
            client.UpdatedAt);

    public static ProductResponse ToResponse(this Product product) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Sku,
            product.Price,
            product.StockQuantity,
            product.MinimumStock,
            product.IsLowStock,
            product.IsActive,
            product.CategoryId,
            product.Category?.Name,
            product.SupplierId,
            product.Supplier?.Name,
            product.CreatedAt,
            product.UpdatedAt);

    public static UserResponse ToResponse(this User user) =>
        new(
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.IsActive,
            user.EmailConfirmed,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt);

    public static MovementResponse ToResponse(this InventoryMovement movement) =>
        new(
            movement.Id,
            movement.MovementType,
            movement.ProductId,
            movement.Product?.Name,
            movement.Product?.Sku,
            movement.Quantity,
            movement.UnitPrice,
            movement.StockBefore,
            movement.StockAfter,
            movement.MovementDate,
            movement.UserId,
            movement.User?.Name,
            movement.ClientId,
            movement.Client?.Name,
            movement.SupplierId,
            movement.Supplier?.Name,
            movement.ReversalOfMovementId,
            movement.Comment);

    public static LowStockItemResponse ToLowStockItem(this Product product) =>
        new(
            product.Id,
            product.Name,
            product.Sku,
            product.StockQuantity,
            product.MinimumStock,
            Math.Max(0, product.MinimumStock - product.StockQuantity),
            product.Category?.Name);
}
