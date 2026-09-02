using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.AuthUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Infrastructure.Security;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

public sealed class LoginTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    private static Register BuildRegister(ApplicationDbContext context, Guid? registrationRoleId) =>
        new(new UserRepository(context), new RoleRepository(context), Hasher,
            BuildIssueTokens(context), context, new StubDefaultRoleProvider(registrationRoleId));

    private static IssueTokens BuildIssueTokens(ApplicationDbContext context) =>
        new(BuildTokenService(), new RefreshTokenRepository(context),
            new StubPermissionResolver(context));

    private static TokenService BuildTokenService() =>
        new(Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "StockHexTests",
            Audience = "StockHexTests",
            Key = "clave-de-pruebas-suficientemente-larga-para-hmac256",
            AccessTokenMinutes = 30
        }));

    [Fact]
    public async Task Credenciales_correctas_devuelven_token_y_actualizan_el_ultimo_ingreso()
    {
        using var context = TestDbContextFactory.Create();
        var user = TestData.User(email: "admin@test.local", passwordHash: Hasher.Hash("Password123"));
        context.Add(user);
        await context.SaveChangesAsync();

        var useCase = new Login(new UserRepository(context), Hasher, BuildIssueTokens(context), context);

        var result = await useCase.RunAsync(new LoginRequest("ADMIN@test.local", "Password123"));

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
        result.Value.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.Value.User.Role.Name.Should().Be("Administrador");
        context.Users.Single().LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Contrasena_incorrecta_no_revela_si_el_email_existe()
    {
        using var context = TestDbContextFactory.Create();
        context.Add(TestData.User(passwordHash: Hasher.Hash("Password123")));
        await context.SaveChangesAsync();

        var useCase = new Login(new UserRepository(context), Hasher, BuildIssueTokens(context), context);

        var wrongPassword = await useCase.RunAsync(new LoginRequest("user@test.local", "otra"));
        var unknownEmail = await useCase.RunAsync(new LoginRequest("nadie@test.local", "otra"));

        wrongPassword.Error!.Type.Should().Be(ErrorType.Unauthorized);
        unknownEmail.Error!.Type.Should().Be(ErrorType.Unauthorized);
        // Mismo mensaje en ambos casos: no se filtra qué emails están registrados.
        wrongPassword.Error.Message.Should().Be(unknownEmail.Error.Message);
    }

    [Fact]
    public async Task Cuenta_desactivada_no_puede_iniciar_sesion()
    {
        using var context = TestDbContextFactory.Create();
        context.Add(TestData.User(passwordHash: Hasher.Hash("Password123"), isActive: false));
        await context.SaveChangesAsync();

        var useCase = new Login(new UserRepository(context), Hasher, BuildIssueTokens(context), context);

        var result = await useCase.RunAsync(new LoginRequest("user@test.local", "Password123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Message.Should().Contain("desactivada");
    }

    [Fact]
    public async Task El_registro_publico_siempre_crea_un_operador()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.OperatorRole();
        context.Add(role);
        await context.SaveChangesAsync();

        var useCase = BuildRegister(context, role.Id);

        var result = await useCase.RunAsync(
            new RegisterRequest("Nuevo", "nuevo@test.local", "Password123", "Password123"));

        result.IsSuccess.Should().BeTrue();
        // Aunque el rol no se pueda enviar, se comprueba que nunca escale a Admin.
        result.Value.User.Role.Name.Should().Be("Bodeguero");
        context.Users.Single().PasswordHash.Should().NotBe("Password123");
    }

    [Fact]
    public async Task El_registro_rechaza_contrasenas_que_no_coinciden()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.OperatorRole();
        context.Add(role);
        await context.SaveChangesAsync();

        var useCase = BuildRegister(context, role.Id);

        var result = await useCase.RunAsync(
            new RegisterRequest("Nuevo", "nuevo@test.local", "Password123", "Password124"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task El_registro_rechaza_un_email_ya_usado()
    {
        using var context = TestDbContextFactory.Create();
        context.Add(TestData.User(email: "ocupado@test.local"));
        await context.SaveChangesAsync();

        var role = TestData.OperatorRole();
        context.Add(role);
        await context.SaveChangesAsync();

        var useCase = BuildRegister(context, role.Id);

        var result = await useCase.RunAsync(
            new RegisterRequest("Nuevo", "OCUPADO@test.local", "Password123", "Password123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }
}
