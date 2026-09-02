using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace StockHex_API.Api.Middleware;

/// <summary>
/// Red de seguridad del pipeline: traduce a ProblemDetails (RFC 7807) las
/// excepciones que emergen de la infraestructura.
///
/// Los errores de negocio esperados (no encontrado, duplicado, stock insuficiente)
/// no llegan aquí: viajan como <see cref="Domain.Common.Result{T}"/> desde las use
/// cases. Este middleware cubre lo que Result no puede expresar, como una violación
/// de constraint por condición de carrera o un fallo inesperado.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // Ya se escribieron cabeceras: no se puede reemplazar la respuesta.
            _logger.LogError(exception, "Excepción después de haber comenzado la respuesta.");
            throw exception;
        }

        var problem = Translate(exception);

        // Los 5xx son fallos nuestros y van como Error; los 4xx son entradas
        // inválidas del cliente y sólo se registran como Warning.
        if (problem.Status >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Error no controlado procesando {Method} {Path}.",
                context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("{Method} {Path} rechazada: {Detail}",
                context.Request.Method, context.Request.Path, problem.Detail);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions),
            context.RequestAborted);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private ProblemDetails Translate(Exception exception) => exception switch
    {
        // Violación de un índice único que no se alcanzó a detectar antes por una
        // condición de carrera entre la comprobación y el insert.
        DbUpdateException { InnerException: not null } ex when IsUniqueViolation(ex) => Create(
            StatusCodes.Status409Conflict,
            "Conflicto",
            "La operación viola una restricción de unicidad; el registro ya existe."),

        // Llega aquí sólo si se agotaron los reintentos de concurrencia.
        DbUpdateConcurrencyException => Create(
            StatusCodes.Status409Conflict,
            "Conflicto de concurrencia",
            "El registro está recibiendo demasiadas modificaciones simultáneas. " +
            "Vuelve a intentarlo en unos instantes."),

        OperationCanceledException => Create(
            StatusCodesExtra.Status499ClientClosedRequest,
            "Petición cancelada",
            "El cliente canceló la petición."),

        _ => Create(
            StatusCodes.Status500InternalServerError,
            "Error interno",
            // El detalle real sólo se expone fuera de producción.
            _environment.IsDevelopment()
                ? exception.ToString()
                : "Ocurrió un error inesperado. Revisa los logs con el traceId indicado.")
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.Message is { } message &&
        (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase));

    private static ProblemDetails Create(int status, string title, string detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail
        };
}

internal static class StatusCodesExtra
{
    /// <summary>Convención de nginx para "el cliente cerró la conexión".</summary>
    public const int Status499ClientClosedRequest = 499;
}
