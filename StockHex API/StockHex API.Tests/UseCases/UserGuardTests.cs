using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>Protege el sistema de quedarse sin administrador o de auto-eliminaciones.</summary>
public sealed class UserGuardTests
{
    [Fact]
    public async Task No_se_puede_degradar_al_unico_administrador()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.User(UserRole.Admin, "admin@test.local");
        context.Add(admin);
        await context.SaveChangesAsync();

        var useCase = new UpdateUser(new UserRepository(context), context);

        var result = await useCase.RunAsync(admin.Id,
            new UpdateUserRequest(admin.Name, admin.Email, UserRole.Operator, true));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Users.Single().Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task No_se_puede_desactivar_al_unico_administrador()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.User(UserRole.Admin, "admin@test.local");
        context.Add(admin);
        await context.SaveChangesAsync();

        var result = await new UpdateUser(new UserRepository(context), context).RunAsync(admin.Id,
            new UpdateUserRequest(admin.Name, admin.Email, UserRole.Admin, false));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Users.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Con_dos_administradores_se_puede_degradar_a_uno()
    {
        using var context = TestDbContextFactory.Create();
        var first = TestData.User(UserRole.Admin, "admin1@test.local");
        var second = TestData.User(UserRole.Admin, "admin2@test.local");
        context.AddRange(first, second);
        await context.SaveChangesAsync();

        var result = await new UpdateUser(new UserRepository(context), context).RunAsync(second.Id,
            new UpdateUserRequest(second.Name, second.Email, UserRole.Manager, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Should().Be(UserRole.Manager);
    }

    [Fact]
    public async Task Un_usuario_no_puede_eliminar_su_propia_cuenta()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.User(UserRole.Admin, "admin@test.local");
        var other = TestData.User(UserRole.Admin, "otro@test.local");
        context.AddRange(admin, other);
        await context.SaveChangesAsync();

        var useCase = new DeleteUser(
            new UserRepository(context),
            new StubCurrentUser(admin.Id, UserRole.Admin),
            context);

        var result = await useCase.RunAsync(admin.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("su propia cuenta");
    }

    [Fact]
    public async Task El_cambio_de_contrasena_exige_la_actual_correcta()
    {
        using var context = TestDbContextFactory.Create();
        var hasher = new StockHex_API.Infrastructure.Security.BCryptPasswordHasher();
        var user = TestData.User(passwordHash: hasher.Hash("Password123"));
        context.Add(user);
        await context.SaveChangesAsync();

        var useCase = new ChangePassword(new UserRepository(context), hasher, context);

        var wrong = await useCase.RunAsync(user.Id,
            new ChangePasswordRequest("incorrecta", "NuevaPass123", "NuevaPass123"));

        wrong.IsFailure.Should().BeTrue();
        wrong.Error!.Type.Should().Be(ErrorType.Validation);

        var ok = await useCase.RunAsync(user.Id,
            new ChangePasswordRequest("Password123", "NuevaPass123", "NuevaPass123"));

        ok.IsSuccess.Should().BeTrue();
        hasher.Verify("NuevaPass123", context.Users.Single().PasswordHash).Should().BeTrue();
    }
}
