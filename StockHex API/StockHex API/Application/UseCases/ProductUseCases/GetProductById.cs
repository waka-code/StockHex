using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ProductUseCases;

public sealed class GetProductById
{
    private readonly IProductRepository _products;

    public GetProductById(IProductRepository products) => _products = products;

    public async Task<Result<ProductResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, includeRelations: true, cancellationToken);

        return product is null
            ? Result<ProductResponse>.Failure(Error.NotFound("Producto", id))
            : product.ToResponse();
    }
}
