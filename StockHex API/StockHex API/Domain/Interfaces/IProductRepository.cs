using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces
{
    public interface IProductRepository
    {
        Task<Product> GetProductById(Guid id);
        Task<IEnumerable<Product>> GetAllProducts();
        Task<Product>PostProduct(Product product);
        Task<Product> UpdateProductById(Product product);
        Task DeleteProductById(Guid id);
    }
}
