using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.CategoryUseCases;

public sealed class GetCategories
{
    private readonly ICategoryRepository _categories;

    public GetCategories(ICategoryRepository categories) => _categories = categories;

    public async Task<Result<PagedResponse<CategoryResponse>>> RunAsync(
        PageRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = await _categories.GetPagedAsync(request, cancellationToken);
        return PagedResponse<CategoryResponse>.From(page, c => c.ToResponse(c.Products.Count));
    }
}
