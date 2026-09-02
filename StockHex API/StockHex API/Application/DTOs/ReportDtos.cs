using StockHex_API.Domain.Enums;

namespace StockHex_API.Application.DTOs;

/// <summary>Foto general del inventario para el dashboard.</summary>
public sealed record InventorySummaryResponse(
    int TotalProducts,
    int ActiveProducts,
    int LowStockProducts,
    decimal TotalStockValue,
    DateTime GeneratedAt);

public sealed record LowStockItemResponse(
    Guid ProductId,
    string Name,
    string Sku,
    int StockQuantity,
    int MinimumStock,
    int Deficit,
    string? CategoryName);

public sealed record MovementSummaryLine(
    MovementType MovementType,
    int Movements,
    int Units);

public sealed record MovementSummaryResponse(
    DateTime From,
    DateTime To,
    IReadOnlyList<MovementSummaryLine> Lines,
    DateTime GeneratedAt);
