namespace StockHex_API.Application.DTOs;

public sealed record CreateClientRequest(
    string Name,
    string? Address,
    string? PhoneNumber,
    string? Email);

public sealed record UpdateClientRequest(
    string Name,
    string? Address,
    string? PhoneNumber,
    string? Email);

public sealed record ClientResponse(
    Guid Id,
    string Name,
    string? Address,
    string? PhoneNumber,
    string? Email,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
