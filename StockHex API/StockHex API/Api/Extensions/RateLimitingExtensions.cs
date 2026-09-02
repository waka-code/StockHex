using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace StockHex_API.Api.Extensions;

/// <summary>Límites del acceso a los endpoints de autenticación.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Intentos permitidos por IP dentro de la ventana.</summary>
    [Range(1, 100_000)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, 3_600)]
    public int AuthWindowSeconds { get; set; } = 60;
}

public static class RateLimitingExtensions
{
    /// <summary>Política aplicada a los endpoints de autenticación.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Limita los intentos contra <c>/api/auth</c> por dirección IP. Sin esto,
    /// el login queda abierto a fuerza bruta: la validación rechaza contraseñas
    /// débiles, pero no limita cuántas se pueden probar.
    /// </summary>
    public static IServiceCollection AddConfiguredRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitingOptions>()
            .Bind(configuration.GetSection(RateLimitingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AuthPolicy, context =>
            {
                // Los límites se resuelven por petición y no al registrar la política:
                // leerlos aquí es lo que permite que la configuración del entorno
                // (o de un test) realmente los sustituya.
                var limits = context.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                return RateLimitPartition.GetFixedWindowLimiter(
                    // Detrás de un proxy hay que reenviar la IP real (UseForwardedHeaders);
                    // si no se puede resolver, todas las peticiones comparten partición.
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.AuthWindowSeconds),
                        // Sin cola: encolar intentos de login sólo retrasa el rechazo.
                        QueueLimit = 0
                    });
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var limits = context.HttpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    // El limitador de ventana fija no siempre expone RetryAfter;
                    // la ventana completa es una cota superior correcta.
                    context.HttpContext.Response.Headers.RetryAfter =
                        limits.AuthWindowSeconds.ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                // Se responde con el mismo formato ProblemDetails que el resto de la API.
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas peticiones",
                    Detail = $"Se permiten {limits.AuthPermitLimit} intentos cada " +
                             $"{limits.AuthWindowSeconds} segundos. Espera antes de reintentar.",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["code"] = "rate_limited";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(problem, JsonOptions),
                    cancellationToken);
            };
        });

        return services;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
