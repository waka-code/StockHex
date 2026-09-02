using Microsoft.AspNetCore.Mvc;
using StockHex_API.Domain.Common;

namespace StockHex_API.Api.Extensions;

/// <summary>
/// Traduce un <see cref="Result"/> del dominio a una respuesta HTTP. Centraliza
/// el mapeo error -> status para que los controladores no lo repitan endpoint a endpoint.
/// </summary>
public static class ResultExtensions
{
    /// <summary>200 con el valor, o el ProblemDetails correspondiente al error.</summary>
    public static IActionResult ToOk<T>(this Result<T> result) =>
        result.IsSuccess
            ? new OkObjectResult(result.Value)
            : Problem(result.Error!);

    /// <summary>201 con cabecera Location apuntando a <paramref name="routeName"/>.</summary>
    public static IActionResult ToCreated<T>(this Result<T> result, string routeName, Func<T, object> routeValues) =>
        result.IsSuccess
            ? new CreatedAtRouteResult(routeName, routeValues(result.Value), result.Value)
            : Problem(result.Error!);

    /// <summary>204 sin cuerpo, para deletes y operaciones sin valor de retorno.</summary>
    public static IActionResult ToNoContent(this Result result) =>
        result.IsSuccess
            ? new NoContentResult()
            : Problem(result.Error!);

    private static IActionResult Problem(Error error)
    {
        var status = StatusFor(error.Type);

        if (error.Type == ErrorType.Validation && error.Errors is { Count: > 0 })
        {
            return new ObjectResult(new ValidationProblemDetails(
                error.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                Status = status,
                Title = "Error de validación",
                Detail = error.Message
            })
            {
                StatusCode = status,
                ContentTypes = { "application/problem+json" }
            };
        }

        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = TitleFor(error.Type),
            Detail = error.Message,
            Extensions = { ["code"] = error.Code }
        })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Error de validación",
        ErrorType.NotFound => "Recurso no encontrado",
        ErrorType.Conflict => "Conflicto",
        ErrorType.Unauthorized => "No autorizado",
        ErrorType.Forbidden => "Acceso denegado",
        _ => "Error interno"
    };
}
