using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.CategoryUseCases;

public sealed class UpdateCategory
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateCategory(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CategoryResponse>> RunAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return Result<CategoryResponse>.Failure(Error.NotFound("Categoría", id));

        var name = request.Name.Trim();

        if (await _categories.ExistsByNameAsync(name, id, cancellationToken))
            return Result<CategoryResponse>.Failure(
                Error.Conflict($"Ya existe otra categoría llamada '{name}'."));

        category.Name = name;
        category.Description = request.Description?.Trim();
        category.UpdatedAt = DateTime.UtcNow;

        _categories.Update(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var productCount = await _categories.CountProductsAsync(id, cancellationToken);
        return category.ToResponse(productCount);
    }
}
