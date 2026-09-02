using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.CategoryUseCases;

public sealed class DeleteCategory
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategory(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return Result.Failure(Error.NotFound("Categoría", id));

        // Los productos exigen una categoría, así que borrarla dejaría filas huérfanas.
        var productCount = await _categories.CountProductsAsync(id, cancellationToken);
        if (productCount > 0)
            return Result.Failure(Error.Conflict(
                $"No se puede eliminar la categoría porque tiene {productCount} producto(s) asociado(s)."));

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
