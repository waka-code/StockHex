using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.AuthUseCases;
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
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
    private static UpdateUser BuildUpdate(
        ApplicationDbContext context, StubActiveUserResolver? activeUsers = null) =>
        new(new UserRepository(context), new RoleRepository(context),
            activeUsers ?? new StubActiveUserResolver(context), context);

    private static DeleteUser BuildDelete(
        ApplicationDbContext context, Guid? currentUserId, StubActiveUserResolver? activeUsers = null) =>
        new(new UserRepository(context), new RoleRepository(context),
            new StubCurrentUser(currentUserId),
            activeUsers ?? new StubActiveUserResolver(context), context);

    private static ChangePassword BuildChangePassword(
        ApplicationDbContext context, BCryptPasswordHasher hasher)
    {
        var tokens = new TokenService(Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "StockHexTests",
            Audience = "StockHexClient",
            Key = "clave-de-pruebas-suficientemente-larga-para-hmac256",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 14
        }));
        var refreshTokens = new RefreshTokenRepository(context);

        return new ChangePassword(
            new UserRepository(context),
            refreshTokens,
            hasher,
            new IssueTokens(tokens, refreshTokens, new StubPermissionResolver(context)),
            context);
    }

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

        var useCase = BuildChangePassword(context, hasher);

        var wrong = await useCase.RunAsync(user.Id,
            new ChangePasswordRequest("incorrecta", "NuevaPass123", "NuevaPass123"));

        wrong.IsFailure.Should().BeTrue();
        wrong.Error!.Type.Should().Be(ErrorType.Validation);

        var ok = await useCase.RunAsync(user.Id,
            new ChangePasswordRequest("Password123", "NuevaPass123", "NuevaPass123"));

        ok.IsSuccess.Should().BeTrue();
        hasher.Verify("NuevaPass123", context.Users.Single().PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Cambiar_la_contrasena_revoca_las_sesiones_anteriores()
    {
        using var context = TestDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();
        var role = TestData.Role();
        var user = TestData.User(role, passwordHash: hasher.Hash("Password123"));
        // Dos sesiones abiertas: el navegador de siempre y la del atacante.
        context.AddRange(role, user,
            new RefreshToken { UserId = user.Id, TokenHash = "sesion-a", ExpiresAt = DateTime.UtcNow.AddDays(14) },
            new RefreshToken { UserId = user.Id, TokenHash = "sesion-b", ExpiresAt = DateTime.UtcNow.AddDays(14) });
        await context.SaveChangesAsync();

        var result = await BuildChangePassword(context, hasher).RunAsync(user.Id,
            new ChangePasswordRequest("Password123", "NuevaPass123", "NuevaPass123"));

        result.IsSuccess.Should().BeTrue();

        var anteriores = context.RefreshTokens
            .Where(t => t.TokenHash == "sesion-a" || t.TokenHash == "sesion-b")
            .ToList();
        anteriores.Should().OnlyContain(t => t.RevokedAt != null,
            "cambiar la contraseña es lo que hace quien cree que le robaron la cuenta");
        anteriores.Should().OnlyContain(t => t.RevokedReason == RevocationReasons.PasswordChanged);
    }

    [Fact]
    public async Task El_par_devuelto_al_cambiar_la_contrasena_sigue_vivo()
    {
        using var context = TestDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();
        var role = TestData.Role();
        var user = TestData.User(role, passwordHash: hasher.Hash("Password123"));
        context.AddRange(role, user,
            new RefreshToken { UserId = user.Id, TokenHash = "sesion-vieja", ExpiresAt = DateTime.UtcNow.AddDays(14) });
        await context.SaveChangesAsync();

        var result = await BuildChangePassword(context, hasher).RunAsync(user.Id,
            new ChangePasswordRequest("Password123", "NuevaPass123", "NuevaPass123"));

        result.IsSuccess.Should().BeTrue();

        // Sin esto el propio dispositivo quedaría zombi: el access token vale hasta
        // que expira, pero su refresco ya no, así que el usuario cae de golpe.
        var emitido = context.RefreshTokens.Single(t => t.RevokedAt == null);
        emitido.UserId.Should().Be(user.Id);
        result.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Value.AccessToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task La_contrasena_nueva_no_puede_ser_la_misma()
    {
        using var context = TestDbContextFactory.Create();
        var hasher = new BCryptPasswordHasher();
        var role = TestData.Role();
        var user = TestData.User(role, passwordHash: hasher.Hash("Password123"));
        context.AddRange(role, user);
        await context.SaveChangesAsync();

        var result = await BuildChangePassword(context, hasher).RunAsync(user.Id,
            new ChangePasswordRequest("Password123", "Password123", "Password123"));

        result.IsFailure.Should().BeTrue("revocaría todas las sesiones sin cambiar nada");
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Desactivar_a_un_usuario_lo_saca_de_inmediato()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        var otro = TestData.User(admin, "admin@test.local");
        var operador = TestData.User(TestData.OperatorRole(), "bodega@test.local");
        context.AddRange(admin, otro, operador.Role!, operador);
        await context.SaveChangesAsync();

        var activeUsers = new StubActiveUserResolver(context);

        var result = await BuildUpdate(context, activeUsers).RunAsync(operador.Id,
            new UpdateUserRequest(operador.Name, operador.Email, operador.RoleId, IsActive: false));

        result.IsSuccess.Should().BeTrue();
        activeUsers.InvalidateCalls.Should().Be(1,
            "su access token sigue firmado y sin expirar; lo que lo corta es invalidar la caché");
        (await activeUsers.IsActiveAsync(operador.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Eliminar_a_un_usuario_tambien_invalida_su_token()
    {
        using var context = TestDbContextFactory.Create();
        var admin = TestData.Role();
        var quienBorra = TestData.User(admin, "admin@test.local");
        var operador = TestData.User(TestData.OperatorRole(), "bodega@test.local");
        context.AddRange(admin, quienBorra, operador.Role!, operador);
        await context.SaveChangesAsync();

        var activeUsers = new StubActiveUserResolver(context);

        var result = await BuildDelete(context, quienBorra.Id, activeUsers).RunAsync(operador.Id);

        result.IsSuccess.Should().BeTrue("no registró movimientos, así que se borra de verdad");
        activeUsers.InvalidateCalls.Should().Be(1);
    }
}
