using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.CategoryUseCases;

public sealed class CreateCategory
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategory(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CategoryResponse>> RunAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _categories.ExistsByNameAsync(name, null, cancellationToken))
            return Result<CategoryResponse>.Failure(
                Error.Conflict($"Ya existe una categoría llamada '{name}'."));

        var category = new Category
        {
            Name = name,
            Description = request.Description?.Trim()
        };

        await _categories.AddAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return category.ToResponse();
    }
}
