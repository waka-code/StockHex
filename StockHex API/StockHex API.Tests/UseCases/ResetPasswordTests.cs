using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Infrastructure.Security;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Restablecer la contraseña de otro usuario. La autorización la impone el
/// permiso <c>users.change_password</c> en el endpoint; aquí se prueban las reglas
/// del caso de uso.
/// </summary>
public sealed class ResetPasswordTests
{
    private static readonly BCryptPasswordHasher Hasher = new();

    private static ResetUserPassword Build(ApplicationDbContext context, Guid? actingUserId) =>
        new(new UserRepository(context), new RefreshTokenRepository(context), Hasher,
            new StubCurrentUser(actingUserId), context);

    private static async Task<(User Target, User Actor)> SeedAsync(ApplicationDbContext context)
    {
        var role = TestData.Role();
        var target = TestData.User(role, "juan@test.local", Hasher.Hash("Antigua123"));
        var actor = TestData.User(role, "admin@test.local", Hasher.Hash("Admin12345"));
        context.AddRange(role, target, actor);
        await context.SaveChangesAsync();
        return (target, actor);
    }

    [Fact]
    public async Task Se_cambia_la_contrasena_sin_pedir_la_actual()
    {
        using var context = TestDbContextFactory.Create();
        var (target, actor) = await SeedAsync(context);

        var result = await Build(context, actor.Id).RunAsync(target.Id,
            new ResetPasswordRequest("NuevaClave123", "NuevaClave123"));

        result.IsSuccess.Should().BeTrue();

        var updated = context.Users.Single(u => u.Id == target.Id);
        Hasher.Verify("NuevaClave123", updated.PasswordHash).Should().BeTrue();
        Hasher.Verify("Antigua123", updated.PasswordHash).Should().BeFalse();
    }

    [Fact]
    public async Task Por_defecto_se_revocan_las_sesiones_del_afectado()
    {
        using var context = TestDbContextFactory.Create();
        var (target, actor) = await SeedAsync(context);

        context.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = "hash-de-una-sesion-abierta",
            UserId = target.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await context.SaveChangesAsync();

        var result = await Build(context, actor.Id).RunAsync(target.Id,
            new ResetPasswordRequest("NuevaClave123", "NuevaClave123"));

        result.IsSuccess.Should().BeTrue();
        // Sin esto, quien tuviera la sesión abierta seguiría dentro con la anterior.
        context.RefreshTokens.Single().RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Se_puede_pedir_que_no_se_revoquen()
    {
        using var context = TestDbContextFactory.Create();
        var (target, actor) = await SeedAsync(context);

        context.RefreshTokens.Add(new RefreshToken
        {
            TokenHash = "hash-de-una-sesion-abierta",
            UserId = target.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
        });
        await context.SaveChangesAsync();

        var result = await Build(context, actor.Id).RunAsync(target.Id,
            new ResetPasswordRequest("NuevaClave123", "NuevaClave123", RevokeSessions: false));

        result.IsSuccess.Should().BeTrue();
        context.RefreshTokens.Single().RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task No_sirve_para_cambiar_la_propia_contrasena()
    {
        using var context = TestDbContextFactory.Create();
        var (_, actor) = await SeedAsync(context);

        // Para la propia cuenta existe el endpoint que sí pide la actual; permitir
        // el atajo aquí sería una forma de saltárselo.
        var result = await Build(context, actor.Id).RunAsync(actor.Id,
            new ResetPasswordRequest("NuevaClave123", "NuevaClave123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("contraseña actual");
    }

    [Fact]
    public async Task Las_contrasenas_deben_coincidir()
    {
        using var context = TestDbContextFactory.Create();
        var (target, actor) = await SeedAsync(context);

        var result = await Build(context, actor.Id).RunAsync(target.Id,
            new ResetPasswordRequest("NuevaClave123", "OtraClave123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Un_usuario_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();
        var (_, actor) = await SeedAsync(context);

        var result = await Build(context, actor.Id).RunAsync(Guid.NewGuid(),
            new ResetPasswordRequest("NuevaClave123", "NuevaClave123"));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
