using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockHex_API.Api.Extensions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.AuthUseCases;

namespace StockHex_API.Api.Controllers;

[ApiController]
[Route("api/auth")]
// Limita los intentos por IP: sin esto el login queda abierto a fuerza bruta.
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public sealed class AuthController : ControllerBase
{
    private readonly Login _login;
    private readonly Register _register;
    private readonly RefreshAccessToken _refresh;
    private readonly Logout _logout;
    private readonly GetCurrentUser _getCurrentUser;

    public AuthController(
        Login login,
        Register register,
        RefreshAccessToken refresh,
        Logout logout,
        GetCurrentUser getCurrentUser)
    {
        _login = login;
        _register = register;
        _refresh = refresh;
        _logout = logout;
        _getCurrentUser = getCurrentUser;
    }

    /// <summary>Autentica al usuario y devuelve el par access + refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken) =>
        (await _login.RunAsync(request, cancellationToken)).ToOk();

    /// <summary>Auto-registro público. El usuario se crea siempre con rol Operator.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken) =>
        (await _register.RunAsync(request, cancellationToken)).ToOk();

    /// <summary>
    /// Canjea un token de refresco por un par nuevo. El token usado queda revocado
    /// (rotación); si se reutiliza uno ya rotado, se invalida la sesión completa.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        (await _refresh.RunAsync(request, cancellationToken)).ToOk();

    /// <summary>
    /// Revoca el token de refresco. Con <c>allSessions=true</c> cierra todas las
    /// sesiones del usuario. El access token en curso sigue válido hasta expirar.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LogoutAsync(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken) =>
        (await _logout.RunAsync(request, cancellationToken)).ToNoContent();

    /// <summary>Perfil del usuario dueño del token, con sus permisos efectivos.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MeAsync(CancellationToken cancellationToken) =>
        (await _getCurrentUser.RunAsync(cancellationToken)).ToOk();
}
