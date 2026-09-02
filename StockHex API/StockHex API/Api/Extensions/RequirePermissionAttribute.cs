using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StockHex_API.Application.Abstractions;

namespace StockHex_API.Api.Extensions;

/// <summary>
/// Exige un permiso del catálogo para ejecutar la acción. Reemplaza a
/// <c>[Authorize(Roles = …)]</c>: con roles configurables, atar un endpoint a un
/// nombre de rol deja de tener sentido; lo que importa es la capacidad.
///
/// El frontend usa los mismos permisos para no ofrecer acciones que van a fallar,
/// pero la autorización la impone este filtro: pedir el endpoint a mano responde
/// 403 igual (regla 5).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
{
    public RequirePermissionAttribute(string permission) => Permission = permission;

    public string Permission { get; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();

        if (!user.IsAuthenticated || user.RoleId is null)
        {
            context.Result = Problem(context, StatusCodes.Status401Unauthorized,
                "No autorizado", "Se requiere autenticación.");
            return;
        }

        var resolver = context.HttpContext.RequestServices.GetRequiredService<IPermissionResolver>();

        if (!await resolver.HasPermissionAsync(user.RoleId.Value, Permission, context.HttpContext.RequestAborted))
        {
            context.Result = Problem(context, StatusCodes.Status403Forbidden,
                "Acceso denegado",
                $"Tu rol no tiene el permiso '{Permission}'.");
        }
    }

    /// <summary>Mismo formato ProblemDetails que el resto de la API.</summary>
    private static ObjectResult Problem(
        AuthorizationFilterContext context,
        int status,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.HttpContext.Request.Path,
        };
        problem.Extensions["code"] = status == StatusCodes.Status403Forbidden
            ? "forbidden"
            : "unauthorized";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
