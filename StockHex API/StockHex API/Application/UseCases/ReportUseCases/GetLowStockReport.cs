using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ReportUseCases;

public sealed class GetLowStockReport
{
    private readonly IProductRepository _products;

    public GetLowStockReport(IProductRepository products) => _products = products;

    public async Task<Result<PagedResponse<LowStockItemResponse>>> RunAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        // Paginado: en un catálogo grande el reporte completo puede ser enorme, y
        // el orden por déficit lo hace la base, así que la página 1 ya trae lo urgente.
        var page = await _products.GetLowStockAsync(request, cancellationToken);

        return PagedResponse<LowStockItemResponse>.From(page, p => p.ToLowStockItem());
    }
}
