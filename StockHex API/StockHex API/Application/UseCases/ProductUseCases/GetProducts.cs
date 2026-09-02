using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ProductUseCases;

public sealed class GetProducts
{
    private readonly IProductRepository _products;

    public GetProducts(IProductRepository products) => _products = products;

    public async Task<Result<PagedResponse<ProductResponse>>> RunAsync(
        ProductFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _products.GetPagedAsync(filter, cancellationToken);
        return PagedResponse<ProductResponse>.From(page, p => p.ToResponse());
    }
}
