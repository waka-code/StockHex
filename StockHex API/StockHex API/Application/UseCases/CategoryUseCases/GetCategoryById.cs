using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.CategoryUseCases
{
    public class GetCategoryById
    {
        private readonly CategoryService _categoryService;

        public GetCategoryById(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task<Category> RunAsync(Guid id)
        {
            var category = await _categoryService.GetCategoryById(id);

            if (category == null)
                throw new KeyNotFoundException($"Category with ID not found: {id}");

            return category;
        }
    }
}
