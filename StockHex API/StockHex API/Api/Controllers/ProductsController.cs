using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.ProductUseCases;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
[RequirePermission(Permissions.Products.View)]
public sealed class ProductsController : ControllerBase
{
    private readonly CreateProduct _create;
    private readonly UpdateProduct _update;
    private readonly DeleteProduct _delete;
    private readonly GetProductById _getById;
    private readonly GetProducts _getAll;

    public ProductsController(
        CreateProduct create,
        UpdateProduct update,
        DeleteProduct delete,
        GetProductById getById,
        GetProducts getAll)
    {
        _create = create;
        _update = update;
        _delete = delete;
        _getById = getById;
        _getAll = getAll;
    }

    /// <summary>
    /// Lista paginada con filtros por categoría, proveedor, estado y stock bajo.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] ProductFilter filter,
        CancellationToken cancellationToken) =>
        (await _getAll.RunAsync(filter, cancellationToken)).ToOk();

    [HttpGet("{id:guid}", Name = nameof(GetProductByIdAsync))]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProductByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _getById.RunAsync(id, cancellationToken)).ToOk();

    /// <summary>
    /// Crea el producto con stock en cero. Para cargar existencias hay que registrar
    /// un movimiento de entrada en <c>POST /api/inventory-movements</c>.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.Products.Create)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken) =>
        (await _create.RunAsync(request, cancellationToken))
            .ToCreated(nameof(GetProductByIdAsync), p => new { id = p.Id });

    /// <summary>Actualiza los datos del producto. No modifica el stock.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Products.Edit)]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken) =>
        (await _update.RunAsync(id, request, cancellationToken)).ToOk();

    /// <summary>
    /// Elimina el producto. Si ya tiene movimientos registrados se desactiva
    /// en lugar de borrarse, para conservar el historial.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Products.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _delete.RunAsync(id, cancellationToken)).ToNoContent();
}
