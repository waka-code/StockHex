using StockHex_API.Domain.Interfaces;
using StockHex_API.Domain.Entities;
using StockHex_API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StockHex_API.Infrastructure.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private readonly ApplicationDbContext _context;

        public ProductRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Product> PostProduct(Product product)
        {
            await _context.Products.AddAsync(product);
            var existingProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Name == product.Name);

            Console.WriteLine("existingProduct", existingProduct);
            if (existingProduct != null)
            {
                existingProduct.Stock_quantity += product.Stock_quantity;
                await _context.SaveChangesAsync();
                return existingProduct;
            }
            else
            {
                await _context.Products.AddAsync(product);
                await _context.SaveChangesAsync();
                return product;
            }
        }

        public async Task<Product> UpdateProductById(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return product;
        }

        public async Task DeleteProductById(Guid id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                _context.Products.Remove(product);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<Product> GetProductById(Guid id)
        {
            return await _context.Products.FindAsync(id);
        }

        public async Task<IEnumerable<Product>> GetAllProducts()
        {
            return await _context.Products.ToListAsync();
        }
    }
}
