namespace StockHex_API.Api.Extensions;

public static class StockHexCorsExtensions
{
    public const string PolicyName = "StockHexCors";

    /// <summary>Separadores admitidos cuando los orígenes vienen en una sola cadena.</summary>
    private static readonly char[] Separators = [';', ','];

    /// <summary>
    /// Registra la política de CORS con los orígenes de <c>Cors:AllowedOrigins</c>.
    /// Antes el pipeline pedía una política llamada "AllowAngularApp" que nunca se
    /// definía, así que la API no devolvía ninguna cabecera CORS.
    /// </summary>
    public static IServiceCollection AddConfiguredCors(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options => options.AddPolicy(PolicyName, policy =>
        {
            // Se resuelve al construir la política, no al registrar el servicio:
            // así la configuración final (incluida la del entorno) es la que manda.
            var origins = ReadOrigins(configuration);

            if (origins.Length == 0)
            {
                // Sin orígenes configurados se permite cualquiera, pero sin credenciales:
                // el navegador rechaza AllowAnyOrigin junto con AllowCredentials.
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
            else
            {
                policy.WithOrigins(origins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        }));

        return services;
    }

    /// <summary>
    /// Admite las dos formas de declarar los orígenes: un arreglo en appsettings
    /// (<c>"AllowedOrigins": ["http://a", "http://b"]</c>) o una sola cadena
    /// separada por <c>;</c> o <c>,</c>. Lo segundo existe porque pasar un arreglo
    /// por variables de entorno obliga a una variable por elemento
    /// (<c>Cors__AllowedOrigins__0</c>, <c>__1</c>, …), que es fácil de equivocar
    /// y fue justo lo que dejó fuera al puerto del frontend.
    /// </summary>
    public static string[] ReadOrigins(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:AllowedOrigins");

        var fromArray = section.Get<string[]>() ?? [];
        if (fromArray.Length > 0)
            return Normalize(fromArray);

        var single = section.Value;
        return string.IsNullOrWhiteSpace(single)
            ? []
            : Normalize(single.Split(Separators, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string[] Normalize(IEnumerable<string> origins) =>
        origins
            .Select(origin => origin.Trim().TrimEnd('/'))
            .Where(origin => origin.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
