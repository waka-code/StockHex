using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.SupplierUseCases;

public sealed class GetSuppliers
{
    private readonly ISupplierRepository _suppliers;

    public GetSuppliers(ISupplierRepository suppliers) => _suppliers = suppliers;

    public async Task<Result<PagedResponse<SupplierResponse>>> RunAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _suppliers.GetPagedAsync(request, cancellationToken);
        return PagedResponse<SupplierResponse>.From(page, s => s.ToResponse(s.Products.Count));
    }
}
