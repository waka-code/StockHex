using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.CategoryUseCases;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
[RequirePermission(Permissions.Categories.View)]
public sealed class CategoriesController : ControllerBase
{
    private readonly CreateCategory _create;
    private readonly UpdateCategory _update;
    private readonly DeleteCategory _delete;
    private readonly GetCategoryById _getById;
    private readonly GetCategories _getAll;

    public CategoriesController(
        CreateCategory create,
        UpdateCategory update,
        DeleteCategory delete,
        GetCategoryById getById,
        GetCategories getAll)
    {
        _create = create;
        _update = update;
        _delete = delete;
        _getById = getById;
        _getAll = getAll;
    }

    /// <summary>Lista paginada de categorías.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CategoryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken) =>
        (await _getAll.RunAsync(request, cancellationToken)).ToOk();

    [HttpGet("{id:guid}", Name = nameof(GetCategoryByIdAsync))]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCategoryByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _getById.RunAsync(id, cancellationToken)).ToOk();

    [HttpPost]
    [RequirePermission(Permissions.Categories.Create)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken) =>
        (await _create.RunAsync(request, cancellationToken))
            .ToCreated(nameof(GetCategoryByIdAsync), c => new { id = c.Id });

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Categories.Edit)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken) =>
        (await _update.RunAsync(id, request, cancellationToken)).ToOk();

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Categories.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _delete.RunAsync(id, cancellationToken)).ToNoContent();
}
