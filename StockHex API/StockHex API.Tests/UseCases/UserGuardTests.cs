using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Infrastructure.Security;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Con roles configurables, «el último administrador» dejó de ser un valor y pasó
/// a ser una capacidad: lo que hay que preservar es que quede algún usuario activo
/// capaz de administrar roles y usuarios. Estos tests fijan ese guardia.
/// </summary>
public sealed class UserGuardTests
{
    private static UpdateUser BuildUpdate(ApplicationDbContext context) =>
        new(new UserRepository(context), new RoleRepository(context), context);

    private static DeleteUser BuildDelete(ApplicationDbContext context, Guid? currentUserId) =>
        new(new UserRepository(context), new RoleRepository(context),
            new StubCurrentUser(currentUserId), context);

    [Fact]
    public async Task No_se_puede_dejar_el_sistema_sin_nadie_que_administre()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();                 // catálogo completo
        var operador = TestData.OperatorRole();      // sin permisos críticos
        var user = TestData.User(admin, "admin@test.local");
        context.AddRange(admin, operador, user);
        await context.SaveChangesAsync();

        // Degradarlo lo deja sin roles.edit ni users.edit, y no hay nadie más.
        var result = await BuildUpdate(context).RunAsync(user.Id,
            new UpdateUserRequest(user.Name, user.Email, operador.Id, true));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain(Permissions.Roles.Edit);
        context.Users.Single().RoleId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task Tampoco_se_puede_desactivar_al_unico_que_administra()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        var user = TestData.User(admin, "admin@test.local");
        context.AddRange(admin, user);
        await context.SaveChangesAsync();

        var result = await BuildUpdate(context).RunAsync(user.Id,
            new UpdateUserRequest(user.Name, user.Email, admin.Id, false));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Users.Single().IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Con_otro_administrador_activo_si_se_puede_degradar()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        var operador = TestData.OperatorRole();
        var first = TestData.User(admin, "admin1@test.local");
        var second = TestData.User(admin, "admin2@test.local");
        context.AddRange(admin, operador, first, second);
        await context.SaveChangesAsync();

        var result = await BuildUpdate(context).RunAsync(second.Id,
            new UpdateUserRequest(second.Name, second.Email, operador.Id, true));

        result.IsSuccess.Should().BeTrue();
        result.Value.Role.Name.Should().Be("Bodeguero");
    }

    [Fact]
    public async Task Un_rol_distinto_que_tambien_administra_cuenta_como_relevo()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        // Un rol personalizado que sí concede los permisos críticos.
        var coadmin = TestData.Role("Copiloto", isSystem: false, permissions:
            [Permissions.Roles.Edit, Permissions.Users.Edit, Permissions.Users.View]);
        var operador = TestData.OperatorRole();

        var user = TestData.User(admin, "admin@test.local");
        var backup = TestData.User(coadmin, "copiloto@test.local");
        context.AddRange(admin, coadmin, operador, user, backup);
        await context.SaveChangesAsync();

        // Degradar al admin es válido porque el copiloto conserva la capacidad.
        var result = await BuildUpdate(context).RunAsync(user.Id,
            new UpdateUserRequest(user.Name, user.Email, operador.Id, true));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Un_usuario_no_puede_eliminar_su_propia_cuenta()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        var me = TestData.User(admin, "admin@test.local");
        var other = TestData.User(admin, "otro@test.local");
        context.AddRange(admin, me, other);
        await context.SaveChangesAsync();

        var result = await BuildDelete(context, me.Id).RunAsync(me.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("su propia cuenta");
    }

    [Fact]
    public async Task Eliminar_al_unico_que_administra_se_rechaza()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        var only = TestData.User(admin, "admin@test.local");
        var operador = TestData.OperatorRole();
        var otro = TestData.User(operador, "otro@test.local");
        context.AddRange(admin, operador, only, otro);
        await context.SaveChangesAsync();

        // Lo borra alguien más para no chocar con el guardia de la propia cuenta.
        var result = await BuildDelete(context, otro.Id).RunAsync(only.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Users.Should().HaveCount(2);
    }

    [Fact]
    public async Task El_cambio_de_contrasena_exige_la_actual_correcta()
    {
        using var context = TestDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();
        var role = TestData.Role();
        var user = TestData.User(role, passwordHash: hasher.Hash("Password123"));
        context.AddRange(role, user);
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
