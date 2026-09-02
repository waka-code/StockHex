using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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
    }
}
