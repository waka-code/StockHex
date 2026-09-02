using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.ClientUseCases;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
[RequirePermission(Permissions.Clients.View)]
public sealed class ClientsController : ControllerBase
{
    private readonly CreateClient _create;
    private readonly UpdateClient _update;
    private readonly DeleteClient _delete;
    private readonly GetClientById _getById;
    private readonly GetClients _getAll;

    public ClientsController(
        CreateClient create,
        UpdateClient update,
        DeleteClient delete,
        GetClientById getById,
        GetClients getAll)
    {
        _create = create;
        _update = update;
        _delete = delete;
        _getById = getById;
        _getAll = getAll;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ClientResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] PageRequest request,
        CancellationToken cancellationToken) =>
        (await _getAll.RunAsync(request, cancellationToken)).ToOk();

    [HttpGet("{id:guid}", Name = nameof(GetClientByIdAsync))]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _getById.RunAsync(id, cancellationToken)).ToOk();

    [HttpPost]
    [RequirePermission(Permissions.Clients.Create)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateClientRequest request,
        CancellationToken cancellationToken) =>
        (await _create.RunAsync(request, cancellationToken))
            .ToCreated(nameof(GetClientByIdAsync), c => new { id = c.Id });

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Clients.Edit)]
    [ProducesResponseType(typeof(ClientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateClientRequest request,
        CancellationToken cancellationToken) =>
        (await _update.RunAsync(id, request, cancellationToken)).ToOk();

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Clients.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _delete.RunAsync(id, cancellationToken)).ToNoContent();
}
