using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.ReportUseCases;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
[RequirePermission(Permissions.Reports.View)]
public sealed class ReportsController : ControllerBase
{
    private readonly GetInventorySummary _summary;
    private readonly GetLowStockReport _lowStock;
    private readonly GetMovementSummary _movementSummary;

    public ReportsController(
        GetInventorySummary summary,
        GetLowStockReport lowStock,
        GetMovementSummary movementSummary)
    {
        _summary = summary;
        _lowStock = lowStock;
        _movementSummary = movementSummary;
    }

    /// <summary>Indicadores generales: totales de productos, stock bajo y valorización.</summary>
    [HttpGet("inventory-summary")]
    [ProducesResponseType(typeof(InventorySummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventorySummaryAsync(CancellationToken cancellationToken) =>
        (await _summary.RunAsync(cancellationToken)).ToOk();

    /// <summary>Productos activos en o por debajo de su stock mínimo, ordenados por déficit.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(PagedResponse<LowStockItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLowStockAsync(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken) =>
        (await _lowStock.RunAsync(request, cancellationToken)).ToOk();

    /// <summary>Actividad por tipo de movimiento. Sin fechas, cubre los últimos 30 días.</summary>
    [HttpGet("movement-summary")]
    [ProducesResponseType(typeof(MovementSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMovementSummaryAsync(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken) =>
        (await _movementSummary.RunAsync(from, to, cancellationToken)).ToOk();
}
