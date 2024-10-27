using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UseProduct
{
    public class GetProductById
    {
        private readonly ProductService _productService;

        public GetProductById(ProductService productService)
        {
            _productService = productService;
        }

        public async Task<Product> RunAsync(Guid id)
        {
            var product = await _productService.GetProductById(id);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID not found: {id}");

            return product;
        }
    }
}
