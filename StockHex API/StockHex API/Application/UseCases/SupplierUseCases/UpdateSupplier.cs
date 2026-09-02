using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.SupplierUseCases;

public sealed class UpdateSupplier
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSupplier(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SupplierResponse>> RunAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
            return Result<SupplierResponse>.Failure(Error.NotFound("Proveedor", id));

        var name = request.Name.Trim();

        if (await _suppliers.ExistsByNameAsync(name, id, cancellationToken))
            return Result<SupplierResponse>.Failure(
                Error.Conflict($"Ya existe otro proveedor llamado '{name}'."));

        supplier.Name = name;
        supplier.Description = request.Description?.Trim();
        supplier.PhoneNumber = request.PhoneNumber?.Trim();
        supplier.Email = request.Email?.Trim().ToLowerInvariant();
        supplier.UpdatedAt = DateTime.UtcNow;

        _suppliers.Update(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var productCount = await _suppliers.CountProductsAsync(id, cancellationToken);
        return supplier.ToResponse(productCount);
    }
}
