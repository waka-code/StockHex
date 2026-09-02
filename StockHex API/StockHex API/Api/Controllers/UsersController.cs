using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockHex_API.Api.Extensions;
using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly CreateUser _create;
    private readonly UpdateUser _update;
    private readonly DeleteUser _delete;
    private readonly GetUserById _getById;
    private readonly GetUsers _getAll;
    private readonly ChangePassword _changePassword;
    private readonly ResetUserPassword _resetPassword;
    private readonly ICurrentUser _currentUser;

    public UsersController(
        CreateUser create,
        UpdateUser update,
        DeleteUser delete,
        GetUserById getById,
        GetUsers getAll,
        ChangePassword changePassword,
        ResetUserPassword resetPassword,
        ICurrentUser currentUser)
    {
        _create = create;
        _update = update;
        _delete = delete;
        _getById = getById;
        _getAll = getAll;
        _changePassword = changePassword;
        _resetPassword = resetPassword;
        _currentUser = currentUser;
    }

    [HttpGet]
    [RequirePermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(PagedResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] UserFilter filter,
        CancellationToken cancellationToken) =>
        (await _getAll.RunAsync(filter, cancellationToken)).ToOk();

    [HttpGet("{id:guid}", Name = nameof(GetUserByIdAsync))]
    [RequirePermission(Permissions.Users.View)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _getById.RunAsync(id, cancellationToken)).ToOk();

    [HttpPost]
    [RequirePermission(Permissions.Users.Create)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken) =>
        (await _create.RunAsync(request, cancellationToken))
            .ToCreated(nameof(GetUserByIdAsync), u => new { id = u.Id });

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Users.Edit)]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken) =>
        (await _update.RunAsync(id, request, cancellationToken)).ToOk();

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Users.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        (await _delete.RunAsync(id, cancellationToken)).ToNoContent();

    /// <summary>
    /// Restablece la contraseña de otro usuario. No pide la actual, porque quien la
    /// cambia no la conoce; de ahí que exija su propio permiso.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(Permissions.Users.ChangePassword)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResetPasswordAsync(
        Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken) =>
        (await _resetPassword.RunAsync(id, request, cancellationToken)).ToNoContent();

    /// <summary>
    /// Cambia la contraseña del usuario autenticado. Sin permiso especial: cada
    /// uno gestiona su propia credencial y aquí sí se exige la contraseña actual.
    ///
    /// Cierra todas las sesiones y devuelve un par de tokens nuevo: el dispositivo
    /// desde el que se cambió continúa, el resto queda fuera. El cliente tiene que
    /// reemplazar los tokens que guarda por los de la respuesta.
    /// </summary>
    [HttpPost("me/change-password")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangeMyPasswordAsync(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (_currentUser.Id is null)
            return Unauthorized();

        return (await _changePassword.RunAsync(_currentUser.Id.Value, request, cancellationToken))
            .ToOk();
    }
}
