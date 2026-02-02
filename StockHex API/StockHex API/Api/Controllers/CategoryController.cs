using Microsoft.AspNetCore.Mvc;
using StockHex_API.Application.UseCases.CategoryUseCases;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {

        private readonly PostCategory _postCategory;
        private readonly UpdateCategoryById _updateCategoryById;
        private readonly DeleteCategoryById _deleteCategoryById;
        private readonly GetCategoryById _getCategoryById;
        private readonly GetAllCategory _getAllCategory;

        public CategoryController(
       PostCategory postCategory,
       UpdateCategoryById updateCategoryById,
       DeleteCategoryById deleteCategoryById,
       GetCategoryById getCategoryById,
       GetAllCategory getAllCategory)
        {
            _postCategory = postCategory;
            _updateCategoryById = updateCategoryById;
            _deleteCategoryById = deleteCategoryById;
            _getCategoryById = getCategoryById;
            _getAllCategory = getAllCategory;
        }

        [HttpGet]
        [Route("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var category = await _getCategoryById.RunAsync(id);
            return Ok(category);
        }

        [HttpGet]
        [Route("all")]
        public async Task<IActionResult> GetAll()
        {
            var category = await _getAllCategory.Run();
            return Ok(category);
        }

        [HttpPost]
        [Route("new")]
        public async Task<IActionResult> Create([FromBody] Category category)
        {
            await _postCategory.Run(category);
            return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
        }

        [HttpPut]
        [Route("update")]
        public async Task<IActionResult> Update(Guid id, [FromBody] Category category)
        {
            category.Id = id;
            await _updateCategoryById.Run(category);
            return NoContent();
        }

        [HttpDelete]
        [Route("delete")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await _deleteCategoryById.Run(id);
            return NoContent();
        }
    }
}
