namespace StockHex_API.Application.DTOs;

public sealed record CreateSupplierRequest(
    string Name,
    string? Description,
    string? PhoneNumber,
    string? Email);

public sealed record UpdateSupplierRequest(
    string Name,
    string? Description,
    string? PhoneNumber,
    string? Email);

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? Description,
    string? PhoneNumber,
    string? Email,
    int ProductCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
