using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.SupplierUseCases;

public sealed class GetSupplierById
{
    private readonly ISupplierRepository _suppliers;

    public GetSupplierById(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task<Result<SupplierResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
            return Result<SupplierResponse>.Failure(Error.NotFound("Proveedor", id));

        var productCount = await _suppliers.CountProductsAsync(id, cancellationToken);
        return supplier.ToResponse(productCount);
    }
}
