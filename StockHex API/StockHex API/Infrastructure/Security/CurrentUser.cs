using System.Security.Claims;
using StockHex_API.Application.Abstractions;

namespace StockHex_API.Infrastructure.Security;

/// <summary>Lee la identidad del JWT de la petición en curso vía <see cref="IHttpContextAccessor"/>.</summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? Id =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public Guid? RoleId =>
        Guid.TryParse(Principal?.FindFirstValue(StockHexClaims.RoleId), out var roleId)
            ? roleId
            : null;

    public string? RoleName => Principal?.FindFirstValue(ClaimTypes.Role);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
}

/// <summary>Claims propios de StockHex, fuera de los registrados.</summary>
public static class StockHexClaims
{
    /// <summary>Id del rol. Es lo único que el token dice sobre autorización.</summary>
    public const string RoleId = "role_id";
}
