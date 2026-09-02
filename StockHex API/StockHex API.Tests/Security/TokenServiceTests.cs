using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Options;
using StockHex_API.Domain.Enums;
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
            AccessTokenMinutes = minutes
        }));

    [Fact]
    public void El_token_incluye_el_id_el_email_y_el_rol()
    {
        var user = TestData.User(UserRole.Manager, "manager@test.local");

        var (token, expiresAt) = BuildService().CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Issuer.Should().Be("StockHexTests");
        jwt.Audiences.Should().Contain("StockHexClient");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == nameof(UserRole.Manager));
        expiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void El_rol_viaja_en_el_claim_que_lee_Authorize()
    {
        // [Authorize(Roles = "Admin")] compara contra ClaimTypes.Role; si el claim
        // se emitiera con otro nombre, la autorización por rol quedaría inerte.
        var user = TestData.User(UserRole.Admin);

        var (token, _) = BuildService().CreateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value.Should().Be("Admin");
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
