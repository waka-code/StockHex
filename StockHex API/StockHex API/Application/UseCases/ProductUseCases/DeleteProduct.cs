using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ProductUseCases;

public sealed class DeleteProduct
{
    private readonly IProductRepository _products;
    private readonly IInventoryMovementRepository _movements;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProduct(
        IProductRepository products,
        IInventoryMovementRepository movements,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _movements = movements;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, includeRelations: false, cancellationToken);
        if (product is null)
            return Result.Failure(Error.NotFound("Producto", id));

        // Un producto con historial no se borra: se desactiva, para no perder la auditoría.
        var movementCount = await _movements.CountByProductAsync(id, cancellationToken);
        if (movementCount > 0)
        {
            if (!product.IsActive)
                return Result.Failure(Error.Conflict(
                    $"El producto tiene {movementCount} movimiento(s) de inventario y ya está desactivado; " +
                    "no puede eliminarse sin perder el historial."));

            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            _products.Update(product);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure(Error.Conflict(
                $"El producto tiene {movementCount} movimiento(s) de inventario, por lo que se desactivó " +
                "en lugar de eliminarse para conservar el historial."));
        }

        _products.Remove(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
