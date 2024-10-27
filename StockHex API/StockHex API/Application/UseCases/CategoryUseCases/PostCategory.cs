using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.CategoryUseCases
{
    public class PostCategory
    {
        private readonly CategoryService _categoryService;

        public PostCategory(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        public async Task Run(Category category)
        {
            if (string.IsNullOrEmpty(category.Name))
                throw new ArgumentException("The category name is required.");

            await _categoryService.PostCategory(category);
        }
    }
}
