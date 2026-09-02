using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace StockHex_API.Api.Extensions;

/// <summary>Confianza en las cabeceras que inyecta un proxy inverso.</summary>
public sealed class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Desactivado por defecto a propósito. Sin proxy delante, confiar en
    /// X-Forwarded-For permitiría que cualquier cliente falsee su IP y con ello
    /// eluda el límite de intentos de login.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>IPs de los proxies de confianza, por ejemplo el balanceador.</summary>
    public string[] KnownProxies { get; set; } = [];

    /// <summary>Redes de confianza en notación CIDR, por ejemplo "10.0.0.0/8".</summary>
    public string[] KnownNetworks { get; set; } = [];

    /// <summary>
    /// Acepta la cabecera de cualquier origen. Sólo es admisible cuando la API no
    /// es alcanzable salvo a través del proxy, porque anula la protección anterior.
    /// </summary>
    public bool TrustAllProxies { get; set; }
}

public static class ForwardedHeadersExtensions
{
    /// <summary>
    /// Hace que <c>RemoteIpAddress</c> sea la IP real del cliente y no la del proxy.
    /// Sin esto, el rate limiting de <c>/api/auth</c> mete a todos los usuarios en
    /// una sola partición: un único atacante bloquearía el login de todo el mundo.
    /// </summary>
    public static IServiceCollection AddConfiguredForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(ForwardedHeadersSettings.SectionName)
                           .Get<ForwardedHeadersSettings>()
                       ?? new ForwardedHeadersSettings();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            if (settings.TrustAllProxies)
            {
                // Listas vacías = no se comprueba el origen de la cabecera.
                options.KnownProxies.Clear();
                options.KnownNetworks.Clear();
                return;
            }

            // Los valores por omisión sólo confían en loopback, que no sirve cuando
            // el proxy es otro contenedor; de ahí la lista explícita.
            options.KnownProxies.Clear();
            options.KnownNetworks.Clear();

            foreach (var proxy in settings.KnownProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                    options.KnownProxies.Add(address);
            }

            foreach (var network in settings.KnownNetworks)
            {
                var parts = network.Split('/', 2);

                if (parts.Length == 2 &&
                    IPAddress.TryParse(parts[0], out var prefix) &&
                    int.TryParse(parts[1], out var length))
                {
                    options.KnownNetworks.Add(
                        new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length));
                }
            }
        });

        return services;
    }

    /// <summary>
    /// Aplica el middleware sólo si está habilitado, y avisa cuando se habilitó sin
    /// declarar de qué proxy se acepta la cabecera: en ese estado no surte efecto.
    /// </summary>
    public static IApplicationBuilder UseConfiguredForwardedHeaders(this WebApplication app)
    {
        var settings = app.Configuration.GetSection(ForwardedHeadersSettings.SectionName)
                           .Get<ForwardedHeadersSettings>()
                       ?? new ForwardedHeadersSettings();

        if (!settings.Enabled)
            return app;

        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(ForwardedHeadersExtensions));

        if (!settings.TrustAllProxies &&
            settings.KnownProxies.Length == 0 &&
            settings.KnownNetworks.Length == 0)
        {
            logger.LogWarning(
                "ForwardedHeaders:Enabled está activo pero no se declaró ningún proxy de " +
                "confianza. Las cabeceras se ignorarán. Configura ForwardedHeaders:KnownProxies " +
                "o ForwardedHeaders:KnownNetworks.");
        }

        if (settings.TrustAllProxies)
        {
            logger.LogWarning(
                "ForwardedHeaders:TrustAllProxies está activo: se acepta X-Forwarded-For de " +
                "cualquier origen. Úsalo sólo si la API no es alcanzable salvo por el proxy.");
        }

        app.UseForwardedHeaders();

        return app;
    }
}
