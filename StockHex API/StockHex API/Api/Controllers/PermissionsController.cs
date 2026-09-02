using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.RoleUseCases;

namespace StockHex_API.Api.Controllers;

/// <summary>
/// Expone el catálogo de permisos, que vive en el código y no tiene tabla. Es la
/// única fuente: el frontend lo consume desde aquí y no lo redeclara.
/// </summary>
[ApiController]
[Route("api/permissions")]
[Authorize]
public sealed class PermissionsController : ControllerBase
{
    private readonly GetPermissionCatalog _catalog;

    public PermissionsController(GetPermissionCatalog catalog) => _catalog = catalog;

    /// <summary>
    /// Cualquier usuario autenticado puede leerlo: es la lista de capacidades que
    /// existen, no las que tiene. Las propias van en <c>GET /api/auth/me</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PermissionCatalogResponse), StatusCodes.Status200OK)]
    public IActionResult Get() => _catalog.Run().ToOk();
}
