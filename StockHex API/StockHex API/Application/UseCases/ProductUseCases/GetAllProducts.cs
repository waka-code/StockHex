using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UseProduct
{
    public class GetAllProducts
    {
        private readonly ProductService _productService;

        public GetAllProducts(ProductService productService)
        {
            _productService = productService;
        }
        public async Task<IEnumerable<Product>> Run()
        {
            return await _productService.GetAllProducts();
        }
    }
}
