using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;


namespace StockHex_API.Domain.Services
{
    public class ProductService : IProductRepository
    {
        private readonly IProductRepository _repository;

        public ProductService(IProductRepository repository)
        {
            _repository = repository;
        }

        public async Task<Product> GetProductById(Guid id)
        {
            var producto = await _repository.GetProductById(id);
            if (producto == null)
                throw new KeyNotFoundException($"Product Not Found: {id}");
            return producto;
        }

        public async Task<IEnumerable<Product>> GetAllProducts()
        {
            return await _repository.GetAllProducts();
        }

        public async Task<Product> PostProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product), "product cannot be null.");
            return await _repository.PostProduct(product);
        }

        public async Task<Product> UpdateProductById(Product product)
        {
            var productExist = await _repository.GetProductById(product.Id);
            if (productExist == null)
                throw new KeyNotFoundException($"Product with ID not found: {product.Id}");
            productExist.Name = product.Name;
            productExist.Description = product.Description;
            productExist.Sku = product.Sku;
            productExist.Price = product.Price;
            productExist.Stock_quantity = product.Stock_quantity;
            productExist.Category_id = product.Category_id;
            productExist.Supplier_id = product.Supplier_id;
            productExist.Update_date = product.Update_date;
            return await _repository.UpdateProductById(productExist);
        }

        public async Task DeleteProductById(Guid id)
        {
            var deleteProduct = await _repository.GetProductById(id);
            if (deleteProduct == null)
                throw new KeyNotFoundException($"Product with ID: {id} was not found.");
            await _repository.DeleteProductById(deleteProduct.Id);
        }
    }
}
