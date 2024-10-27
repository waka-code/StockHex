using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.ProductUseCases
{
    public class UpdateProductById
    {
        private readonly ProductService _productService;

        public UpdateProductById(ProductService productService)
        {
            _productService = productService;
        }
        public async Task Run(Product product)
        {
            var product_exist = await _productService.GetProductById(product.Id);

            if (product_exist == null)
                throw new KeyNotFoundException($"Prodcut not found: {product.Id}");

            product_exist.Name = product.Name;
            product_exist.Descripcion = product.Descripcion;
            product_exist.Sku = product.Sku;
            product_exist.Price = product.Price;
            product_exist.Stock_quantity = product.Stock_quantity;
            product_exist.Category_id = product.Category_id;
            product_exist.Supplier_id = product.Supplier_id;
            product_exist.Update_date = product.Update_date;

            await _productService.UpdateProductById(product_exist);


        }

    }
}
