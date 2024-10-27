using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Domain.Services
{
    public class CategoryService
    {
        private readonly ICategoryRepository _repository;
        public CategoryService(ICategoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<Category> GetCategoryById(Guid id)
        {
            var category = await _repository.GetCategoryById(id);
            if (category == null)
                throw new KeyNotFoundException($"Category Not Found: {id}");
            return category;
        }

        public async Task<IEnumerable<Category>> GetAllCategory()
        {
            return await _repository.GetAllCategory();
        }

        public async Task<Category> PostCategory(Category category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category), "Category cannot be null.");
            return await _repository.PostCategory(category);
        }

        public async Task<Category> UpdateCategoryById(Category category)
        {
            var categoryExist = await _repository.GetCategoryById(category.Id);
            if (categoryExist == null)
                throw new KeyNotFoundException($"Category with ID not found: {category.Id}");
            categoryExist.Name = category.Name;
            categoryExist.Description = category.Description;
            return await _repository.UpdateCategoryById(categoryExist);
        }

        public async Task DeleteCategoryById(Guid id)
        {
            var deleteCategory = await _repository.GetCategoryById(id);
            if (deleteCategory == null)
                throw new KeyNotFoundException($"Category with ID: {id} was not found.");
            await _repository.DeleteCategoryById(deleteCategory.Id);
        }
    }
   
}
