using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.AuthUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Infrastructure.Security;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Cubre la rotación del token de refresco y la revocación, que es lo que evita
/// que el usuario tenga que volver a autenticarse cada hora sin abrir un agujero.
/// </summary>
public sealed class RefreshTokenTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    private static TokenService BuildTokenService() =>
        new(Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "StockHexTests",
            Audience = "StockHexClient",
            Key = "clave-de-pruebas-suficientemente-larga-para-hmac256",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 14
        }));

    private static Login BuildLogin(ApplicationDbContext context) =>
        new(new UserRepository(context), Hasher,
            new IssueTokens(BuildTokenService(), new RefreshTokenRepository(context),
                new StubPermissionResolver(context)), context);

    private static RefreshAccessToken BuildRefresh(ApplicationDbContext context)
    {
        var tokenService = BuildTokenService();
        var repository = new RefreshTokenRepository(context);

        return new RefreshAccessToken(
            repository,
            new UserRepository(context),
            tokenService,
            new IssueTokens(tokenService, repository, new StubPermissionResolver(context)),
            context);
    }

    private static Logout BuildLogout(ApplicationDbContext context, Guid? currentUserId) =>
        new(new RefreshTokenRepository(context), BuildTokenService(),
            new StubCurrentUser(currentUserId), context);

    private static async Task<(User User, AuthResponse Auth)> LoginAsync(ApplicationDbContext context)
    {
        var user = TestData.User(email: "admin@test.local", passwordHash: Hasher.Hash("Password123"));
        context.Add(user);
        await context.SaveChangesAsync();

        var result = await BuildLogin(context).RunAsync(
            new LoginRequest(user.Email, "Password123"));

        result.IsSuccess.Should().BeTrue();
        return (user, result.Value);
    }

    [Fact]
    public async Task El_login_entrega_un_token_de_refresco_y_solo_guarda_su_hash()
    {
        using var context = TestDbContextFactory.Create();
        var (_, auth) = await LoginAsync(context);

        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshTokenExpiresAt.Should().BeAfter(auth.ExpiresAt,
            "el refresco tiene que durar más que el access token");

        var stored = context.RefreshTokens.Single();
        stored.TokenHash.Should().NotBe(auth.RefreshToken,
            "en la base sólo debe quedar el hash, nunca el token en claro");
        stored.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task El_canje_devuelve_un_par_nuevo_y_revoca_el_anterior()
    {
        using var context = TestDbContextFactory.Create();
        var (_, auth) = await LoginAsync(context);

        var result = await BuildRefresh(context).RunAsync(new RefreshTokenRequest(auth.RefreshToken));

        result.IsSuccess.Should().BeTrue();
        result.Value.RefreshToken.Should().NotBe(auth.RefreshToken, "el token debe rotar");
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();

        var tokens = context.RefreshTokens.ToList();
        tokens.Should().HaveCount(2);

        var original = tokens.Single(t => t.TokenHash == BuildTokenService().HashRefreshToken(auth.RefreshToken));
        original.RevokedAt.Should().NotBeNull();
        original.RevokedReason.Should().Be(RevocationReasons.Rotated);
        original.ReplacedByTokenId.Should().NotBeNull("la cadena de rotación debe quedar enlazada");
    }

    [Fact]
    public async Task Reutilizar_un_token_ya_rotado_invalida_la_sesion_completa()
    {
        using var context = TestDbContextFactory.Create();
        var (_, auth) = await LoginAsync(context);

        // Rotación normal: auth.RefreshToken queda revocado.
        var rotated = await BuildRefresh(context).RunAsync(new RefreshTokenRequest(auth.RefreshToken));
        rotated.IsSuccess.Should().BeTrue();

        // Alguien reutiliza el token viejo: señal de que tiene una copia robada.
        var reused = await BuildRefresh(context).RunAsync(new RefreshTokenRequest(auth.RefreshToken));

        reused.IsFailure.Should().BeTrue();
        reused.Error!.Type.Should().Be(ErrorType.Unauthorized);

        // Y el token que se había emitido en la rotación también queda inutilizado.
        context.RefreshTokens.Should().OnlyContain(t => t.RevokedAt != null);

        var afterBreach = await BuildRefresh(context).RunAsync(
            new RefreshTokenRequest(rotated.Value.RefreshToken));
        afterBreach.IsFailure.Should().BeTrue("la sesión entera se cortó");
    }

    [Fact]
    public async Task Un_token_inexistente_se_rechaza()
    {
        using var context = TestDbContextFactory.Create();
        await LoginAsync(context);

        var result = await BuildRefresh(context).RunAsync(new RefreshTokenRequest("token-inventado"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Un_token_expirado_se_rechaza()
    {
        using var context = TestDbContextFactory.Create();
        var (_, auth) = await LoginAsync(context);

        var stored = context.RefreshTokens.Single();
        stored.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await context.SaveChangesAsync();

        var result = await BuildRefresh(context).RunAsync(new RefreshTokenRequest(auth.RefreshToken));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Un_usuario_desactivado_no_puede_renovar()
    {
        using var context = TestDbContextFactory.Create();
        var (user, auth) = await LoginAsync(context);

        user.IsActive = false;
        await context.SaveChangesAsync();

        var result = await BuildRefresh(context).RunAsync(new RefreshTokenRequest(auth.RefreshToken));

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("desactivada");
        context.RefreshTokens.Single().RevokedReason.Should().Be(RevocationReasons.UserDisabled);
    }

    [Fact]
    public async Task El_logout_revoca_el_token_y_lo_deja_inservible()
    {
        using var context = TestDbContextFactory.Create();
        var (user, auth) = await LoginAsync(context);

        var result = await BuildLogout(context, user.Id)
            .RunAsync(new LogoutRequest(auth.RefreshToken));

        result.IsSuccess.Should().BeTrue();
        context.RefreshTokens.Single().RevokedReason.Should().Be(RevocationReasons.LoggedOut);

        var afterLogout = await BuildRefresh(context).RunAsync(new RefreshTokenRequest(auth.RefreshToken));
        afterLogout.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task El_logout_de_todas_las_sesiones_revoca_los_tokens_de_otros_dispositivos()
    {
        using var context = TestDbContextFactory.Create();
        var (user, first) = await LoginAsync(context);

        // Segundo inicio de sesión: simula otro dispositivo.
        var second = await BuildLogin(context).RunAsync(new LoginRequest(user.Email, "Password123"));
        second.IsSuccess.Should().BeTrue();
        context.RefreshTokens.Should().HaveCount(2);

        var result = await BuildLogout(context, user.Id)
            .RunAsync(new LogoutRequest(first.RefreshToken, AllSessions: true));

        result.IsSuccess.Should().BeTrue();
        context.RefreshTokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task No_se_puede_cerrar_la_sesion_de_otro_usuario()
    {
        using var context = TestDbContextFactory.Create();
        var (_, auth) = await LoginAsync(context);

        // Un usuario autenticado distinto al dueño del token.
        var result = await BuildLogout(context, Guid.NewGuid())
            .RunAsync(new LogoutRequest(auth.RefreshToken));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        context.RefreshTokens.Single().RevokedAt.Should().BeNull();
    }
}
