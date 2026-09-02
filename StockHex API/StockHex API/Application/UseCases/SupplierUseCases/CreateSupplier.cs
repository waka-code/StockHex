using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.SupplierUseCases;

public sealed class CreateSupplier
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSupplier(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<SupplierResponse>> RunAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _suppliers.ExistsByNameAsync(name, null, cancellationToken))
            return Result<SupplierResponse>.Failure(
                Error.Conflict($"Ya existe un proveedor llamado '{name}'."));

        var supplier = new Supplier
        {
            Name = name,
            Description = request.Description?.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Email = request.Email?.Trim().ToLowerInvariant()
        };

        await _suppliers.AddAsync(supplier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return supplier.ToResponse();
    }
}
