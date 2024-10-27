using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.ProductUseCases
{
    public class DeleteProductById
    {
        private readonly ProductService _productService;

        public DeleteProductById(ProductService productService)
        {
            _productService = productService;
        }

        public async Task Run(Guid id)
        {
            var product = await _productService.GetProductById(id);

            if (product == null)
                throw new KeyNotFoundException($"Product with ID not found: {id}");

            await _productService.DeleteProductById(id);

        }
    }
}
