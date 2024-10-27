using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.CategoryUseCases
{
    public class DeleteCategoryById
    {
        private readonly CategoryService _categoryService;


        public DeleteCategoryById(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }
        public async Task Run(Guid id)
        {
            var category = await _categoryService.GetCategoryById(id);

            if (category == null)
                throw new KeyNotFoundException($"Category with ID not found: {id}");

            await _categoryService.DeleteCategoryById(id);

        }

    }
}
