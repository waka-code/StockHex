using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.CategoryUseCases
{
    public class UpdateCategoryById
    {
        private readonly CategoryService _categoryService;

        public UpdateCategoryById(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }
        public async Task Run(Category category)
        {
            var category_exist = await _categoryService.GetCategoryById(category.Id);

            if (category_exist == null)
                throw new KeyNotFoundException($"Prodcut not found: {category.Id}");

            category_exist.Name = category.Name;
            category_exist.Description = category.Description;

            await _categoryService.UpdateCategoryById(category_exist);


        }

    }
}
