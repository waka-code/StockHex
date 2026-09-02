using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StockHex_API.Application.Abstractions;

namespace StockHex_API.Infrastructure.Security;

/// <summary>
/// Configura la validación del bearer leyendo <see cref="JwtOptions"/> por
/// <see cref="IOptions{T}"/>, es decir en el momento de resolverlo y no al registrar.
/// Es la misma fuente que usa <see cref="TokenService"/> para firmar: si se leyera
/// la configuración al registrar, se podría firmar con una clave y validar con otra.
/// </summary>
public sealed class JwtBearerOptionsSetup : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly JwtOptions _jwt;

    public JwtBearerOptionsSetup(IOptions<JwtOptions> jwt) => _jwt = jwt.Value;

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        Configure(options);
    }

    public void Configure(JwtBearerOptions options)
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            ValidateLifetime = true,
            // Sin tolerancia: un token expirado se rechaza de inmediato.
            ClockSkew = TimeSpan.Zero
        };

        // La firma sólo prueba que el token lo emitimos nosotros, no que la cuenta
        // siga existiendo. Se comprueba aquí, en el único punto por el que pasa
        // toda petición autenticada, y no en [RequirePermission]: así también
        // quedan cubiertos los endpoints que sólo llevan [Authorize].
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!Guid.TryParse(id, out var userId))
                {
                    context.Fail("El token no identifica a ningún usuario.");
                    return;
                }

                var users = context.HttpContext.RequestServices
                    .GetRequiredService<IActiveUserResolver>();

                if (!await users.IsActiveAsync(userId, context.HttpContext.RequestAborted))
                    context.Fail("La cuenta está desactivada o ya no existe.");
            }
        };
    }
}
