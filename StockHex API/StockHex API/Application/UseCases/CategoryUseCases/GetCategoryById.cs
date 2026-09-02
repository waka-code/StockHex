using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.CategoryUseCases;

public sealed class GetCategoryById
{
    private readonly ICategoryRepository _categories;

    public GetCategoryById(ICategoryRepository categories) => _categories = categories;

    public async Task<Result<CategoryResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return Result<CategoryResponse>.Failure(Error.NotFound("Categoría", id));

        var productCount = await _categories.CountProductsAsync(id, cancellationToken);
        return category.ToResponse(productCount);
    }
}
