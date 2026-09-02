using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.SupplierUseCases;

public sealed class DeleteSupplier
{
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSupplier(ISupplierRepository suppliers, IUnitOfWork unitOfWork)
    {
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _suppliers.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
            return Result.Failure(Error.NotFound("Proveedor", id));

        var productCount = await _suppliers.CountProductsAsync(id, cancellationToken);
        if (productCount > 0)
            return Result.Failure(Error.Conflict(
                $"No se puede eliminar el proveedor porque tiene {productCount} producto(s) asociado(s)."));

        _suppliers.Remove(supplier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
