using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.CategoryUseCases
{
    public class GetAllCategory
    {
        private readonly CategoryService _categoryService;

        public GetAllCategory(CategoryService categoryService)
        {
            _categoryService = categoryService;
        }
        public async Task<IEnumerable<Category>> Run()
        {
            return await _categoryService.GetAllCategory();
        }
    }
}
