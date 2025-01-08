using Microsoft.AspNetCore.Mvc;
using StockHex_API.Application.UseCases.ProductUseCases;
using StockHex_API.Application.UseCases.UseProduct;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly PostProduct _postProduct;
        private readonly UpdateProductById _updateProductById;
        private readonly DeleteProductById _deleteProductById;
        private readonly GetProductById _getProductById;
        private readonly GetAllProducts _getAllProducts;

        public ProductController(
        PostProduct postProduct,
        UpdateProductById updateProductById,
        DeleteProductById deleteProductById,
        GetProductById getProductById,
        GetAllProducts getAllProducts)
        {
            _postProduct = postProduct;
            _updateProductById = updateProductById;
            _deleteProductById = deleteProductById;
            _getProductById = getProductById;
            _getAllProducts = getAllProducts;
        }


        [HttpGet]
        [Route("${id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var product = await _getProductById.RunAsync(id);
            return Ok(product);
        }

        [HttpGet]
        [Route("all")]
        public async Task<IActionResult> GetAll()
        {
            var products = await _getAllProducts.Run();
            return Ok(products);
        }

        [HttpPost]
        [Route("new")]
        public async Task<IActionResult> Create([FromBody] Product product)
        {
            await _postProduct.Run(product);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }

        [HttpPut]
        [Route("update")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Product product)
        {
            product.Id = id;
            await _updateProductById.Run(product);
            return NoContent();
        }

        [HttpDelete]
        [Route("delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _deleteProductById.Run(id);
            return NoContent();
        }
    }

 
}
