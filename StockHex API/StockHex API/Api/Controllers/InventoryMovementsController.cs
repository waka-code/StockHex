using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.InventoryMovementUseCases;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

/// <summary>
/// Movimientos de inventario: la única vía por la que cambia el stock de un producto.
/// </summary>
[ApiController]
[Route("api/inventory-movements")]
[Authorize]
[RequirePermission(Permissions.Movements.View)]
public sealed class InventoryMovementsController : ControllerBase
{
    private readonly CreateMovement _create;
    private readonly ReverseMovement _reverse;
    private readonly GetMovements _getAll;
    private readonly GetMovementById _getById;

    public InventoryMovementsController(
        CreateMovement create,
        ReverseMovement reverse,
        GetMovements getAll,
        GetMovementById getById)
    {
        _create = create;
        _reverse = reverse;
        _getAll = getAll;
        _getById = getById;
    }

    /// <summary>Historial paginado, filtrable por producto, cliente, proveedor, usuario, tipo y fechas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<MovementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] MovementFilter filter,
        CancellationToken cancellationToken) =>
        (await _getAll.RunAsync(filter, cancellationToken)).ToOk();

    [HttpGet("{id:guid}", Name = nameof(GetMovementByIdAsync))]
    [ProducesResponseType(typeof(MovementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMovementByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _getById.RunAsync(id, cancellationToken)).ToOk();

    /// <summary>
    /// Registra un movimiento y ajusta el stock del producto de forma atómica.
    /// In suma, Out resta (y falla si el stock es insuficiente) y Adjustment fija
    /// el stock al valor indicado en Quantity. El autor se toma del token.
    /// </summary>
    [HttpPost]
    [RequirePermission(Permissions.Movements.Create)]
    [ProducesResponseType(typeof(MovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateMovementRequest request,
        CancellationToken cancellationToken) =>
        (await _create.RunAsync(request, cancellationToken))
            .ToCreated(nameof(GetMovementByIdAsync), m => new { id = m.Id });

    /// <summary>
    /// Corrige un movimiento equivocado registrando el movimiento inverso. El
    /// original no se edita ni se borra: error y corrección quedan en el historial.
    /// Sólo se puede revertir una vez, y no se puede revertir una reversión.
    /// </summary>
    [HttpPost("{id:guid}/reverse")]
    [RequirePermission(Permissions.Movements.Reverse)]
    [ProducesResponseType(typeof(MovementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReverseAsync(
        Guid id,
        [FromBody] ReverseMovementRequest request,
        CancellationToken cancellationToken) =>
        (await _reverse.RunAsync(id, request, cancellationToken))
            .ToCreated(nameof(GetMovementByIdAsync), m => new { id = m.Id });
}
