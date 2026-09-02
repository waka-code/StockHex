using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.RoleUseCases;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

/// <summary>
/// Los roles son datos: se crean, editan y eliminan. El catálogo de permisos, en
/// cambio, vive en el código y se expone en <c>GET /api/permissions</c>.
/// </summary>
[ApiController]
[Route("api/roles")]
[Authorize]
public sealed class RolesController : ControllerBase
{
    private readonly GetRoles _getAll;
    private readonly GetRoleById _getById;
    private readonly CreateRole _create;
    private readonly UpdateRole _update;
    private readonly DeleteRole _delete;

    public RolesController(
        GetRoles getAll,
        GetRoleById getById,
        CreateRole create,
        UpdateRole update,
        DeleteRole delete)
    {
        _getAll = getAll;
        _getById = getById;
        _create = create;
        _update = update;
        _delete = delete;
    }

    [HttpGet]
    [RequirePermission(Permissions.Roles.View)]
    [ProducesResponseType(typeof(PagedResponse<RoleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken) =>
        (await _getAll.RunAsync(request, cancellationToken)).ToOk();

    [HttpGet("{id:guid}", Name = nameof(GetRoleByIdAsync))]
    [RequirePermission(Permissions.Roles.View)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoleByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _getById.RunAsync(id, cancellationToken)).ToOk();

    [HttpPost]
    [RequirePermission(Permissions.Roles.Create)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken) =>
        (await _create.RunAsync(request, cancellationToken))
            .ToCreated(nameof(GetRoleByIdAsync), r => new { id = r.Id });

    /// <summary>
    /// Reemplaza nombre, descripción y el conjunto completo de permisos. El efecto
    /// es inmediato: la caché de permisos del rol se invalida al guardar.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Roles.Edit)]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken) =>
        (await _update.RunAsync(id, request, cancellationToken)).ToOk();

    /// <summary>
    /// Elimina el rol. Falla con 409 si es de sistema o si tiene usuarios asignados.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Roles.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _delete.RunAsync(id, cancellationToken)).ToNoContent();
}
