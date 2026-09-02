using Microsoft.OpenApi.Models;

namespace StockHex_API.Api.Extensions;

public static class SwaggerServiceCollectionExtensions
{
    /// <summary>Swagger con el esquema Bearer, para poder probar endpoints protegidos desde la UI.</summary>
    public static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "StockHex API",
                Version = "v1",
                Description =
                    "API de gestión de inventario. El stock sólo cambia a través de " +
                    "/api/inventory-movements, de modo que todo movimiento queda auditado."
            });

            var scheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Pega aquí el accessToken devuelto por POST /api/auth/login.",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", scheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [scheme] = Array.Empty<string>() });
        });

        return services;
    }
}
