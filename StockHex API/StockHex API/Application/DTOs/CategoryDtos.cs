namespace StockHex_API.Application.DTOs;

public sealed record CreateCategoryRequest(string Name, string? Description);

public sealed record UpdateCategoryRequest(string Name, string? Description);

public sealed record CategoryResponse(
    Guid Id,
    string Name,
    string? Description,
    int ProductCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
