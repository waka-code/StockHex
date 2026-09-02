using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ReportUseCases;

/// <summary>Indicadores generales del inventario. Todos los totales se calculan en la base de datos.</summary>
public sealed class GetInventorySummary
{
    private readonly IProductRepository _products;

    public GetInventorySummary(IProductRepository products) => _products = products;

    public async Task<Result<InventorySummaryResponse>> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var total = await _products.CountAsync(onlyActive: false, cancellationToken);
        var active = await _products.CountAsync(onlyActive: true, cancellationToken);
        var lowStock = await _products.CountLowStockAsync(cancellationToken);
        var stockValue = await _products.GetTotalStockValueAsync(cancellationToken);

        return new InventorySummaryResponse(
            total,
            active,
            lowStock,
            stockValue,
            DateTime.UtcNow);
    }
}
