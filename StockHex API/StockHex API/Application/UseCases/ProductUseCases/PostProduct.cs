using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UseProduct
{
    public class PostProduct
    {
        private readonly ProductService _productService;

        public PostProduct(ProductService productService)
        {
            _productService = productService;
        }

        public async Task Run(Product product)
        {
            if (string.IsNullOrEmpty(product.Name))
                throw new ArgumentException("The product name is required.");

            await _productService.PostProduct(product);
        }
    }
}
