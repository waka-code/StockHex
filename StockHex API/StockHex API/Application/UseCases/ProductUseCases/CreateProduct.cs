using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.ProductUseCases;

public sealed class CreateProduct
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly ISupplierRepository _suppliers;
    private readonly IUnitOfWork _unitOfWork;

    public CreateProduct(
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
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();

        if (await _products.ExistsBySkuAsync(sku, null, cancellationToken))
            return Result<ProductResponse>.Failure(
                Error.Conflict($"Ya existe un producto con el SKU '{sku}'."));

        // Se valida la existencia de las FK aquí para devolver 400/404 claros
        // en lugar de dejar que la base de datos lance un error de constraint.
        if (await _categories.GetByIdAsync(request.CategoryId, cancellationToken) is null)
            return Result<ProductResponse>.Failure(Error.NotFound("Categoría", request.CategoryId));

        if (request.SupplierId.HasValue &&
            !await _suppliers.ExistsAsync(request.SupplierId.Value, cancellationToken))
            return Result<ProductResponse>.Failure(Error.NotFound("Proveedor", request.SupplierId.Value));

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Sku = sku,
            Price = request.Price,
            MinimumStock = request.MinimumStock,
            // El stock arranca en cero: sólo los movimientos de inventario lo alteran.
            StockQuantity = 0,
            CategoryId = request.CategoryId,
            SupplierId = request.SupplierId
        };

        await _products.AddAsync(product, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await _products.GetByIdAsync(product.Id, includeRelations: true, cancellationToken);
        return (created ?? product).ToResponse();
    }
}
