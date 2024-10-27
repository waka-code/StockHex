using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces
{
    public interface ICategoryRepository
    {
        Task<Category> GetCategoryById(Guid id);
        Task<IEnumerable<Category>> GetAllCategory();
        Task<Category> PostCategory(Category category);
        Task<Category> UpdateCategoryById(Category category);
        Task DeleteCategoryById(Guid id);
    }
}
