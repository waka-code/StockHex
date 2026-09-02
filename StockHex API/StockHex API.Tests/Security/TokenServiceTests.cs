using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using StockHex_API.Infrastructure.Security;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.Security;

public sealed class TokenServiceTests
{
    private static TokenService BuildService(int minutes = 60) =>
        new(Options.Create(new JwtOptions
        {
            Issuer = "StockHexTests",
            Audience = "StockHexClient",
            Key = "clave-de-pruebas-suficientemente-larga-para-hmac256",
            AccessTokenMinutes = minutes,
        }));

    [Fact]
    public void El_token_incluye_el_id_del_usuario_el_email_y_el_id_del_rol()
    {
        var role = TestData.Role("Jefe de bodega", isSystem: false);
        var user = TestData.User(role, "manager@test.local");

        var (token, expiresAt) = BuildService().CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("StockHexTests");
        jwt.Audiences.Should().Contain("StockHexClient");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == StockHexClaims.RoleId && c.Value == role.Id.ToString());
        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void El_token_no_lleva_la_lista_de_permisos()
    {
        // Si los llevara, quitarle un permiso a alguien no surtiría efecto hasta
        // que su token se renovara: hasta 60 minutos de desfase.
        var role = TestData.Role();
        var user = TestData.User(role);

        var (token, _) = BuildService().CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Should().NotContain(c => c.Value.Contains('.') && c.Value.Contains("products"),
            "los permisos se resuelven por petición, no viajan en el token");
        jwt.Claims.Count(c => c.Type == StockHexClaims.RoleId).Should().Be(1);
    }

    [Fact]
    public void El_nombre_del_rol_viaja_solo_para_mostrar()
    {
        var role = TestData.Role("Auditor", isSystem: false);
        var user = TestData.User(role);

        var (token, _) = BuildService().CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("Auditor");
    }

    [Fact]
    public void Dos_tokens_del_mismo_usuario_tienen_jti_distinto()
    {
        var user = TestData.User();
        var service = BuildService();

        var first = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(user).Token);
        var second = new JwtSecurityTokenHandler().ReadJwtToken(service.CreateAccessToken(user).Token);

        first.Claims.Single(c => c.Type == "jti").Value
            .Should().NotBe(second.Claims.Single(c => c.Type == "jti").Value);
    }
}
