using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ProductUseCases;

public sealed class UpdateProduct
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProduct(
        IProductRepository products,
        ICategoryRepository categories,
        ISupplierRepository suppliers,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _categories = categories;
        _suppliers = suppliers;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ProductResponse>> RunAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(id, includeRelations: false, cancellationToken);
        if (product is null)
            return Result<ProductResponse>.Failure(Error.NotFound("Producto", id));

        var sku = request.Sku.Trim().ToUpperInvariant();

        if (await _products.ExistsBySkuAsync(sku, id, cancellationToken))
            return Result<ProductResponse>.Failure(
                Error.Conflict($"Ya existe otro producto con el SKU '{sku}'."));

        if (await _categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            return Result<ProductResponse>.Failure(Error.NotFound("Categoría", request.CategoryId));

        if (request.SupplierId.HasValue &&
            !await _suppliers.ExistsAsync(request.SupplierId.Value, cancellationToken))
            return Result<ProductResponse>.Failure(Error.NotFound("Proveedor", request.SupplierId.Value));

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Sku = sku;
        product.Price = request.Price;
        product.MinimumStock = request.MinimumStock;
        product.CategoryId = request.CategoryId;
        product.SupplierId = request.SupplierId;
        product.IsActive = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        // StockQuantity se omite a propósito: cambiarlo aquí saltearía la auditoría.

        _products.Update(product);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var updated = await _products.GetByIdAsync(id, includeRelations: true, cancellationToken);
        return (updated ?? product).ToResponse();
    }
}
