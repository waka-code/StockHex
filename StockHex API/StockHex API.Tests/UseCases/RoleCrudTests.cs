using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.RoleUseCases;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Los roles son datos y se administran desde la interfaz, así que los guardias
/// que evitan dejarse fuera del sistema viven aquí.
/// </summary>
public sealed class RoleCrudTests
{
    /// <summary>
    /// Quien llama tiene, por defecto, el catálogo completo: es lo que necesita la
    /// mayoría de los tests para no chocar con el guardia de escalada. Su rol se
    /// sirve desde el resolver y no como fila, porque varios tests afirman cuántos
    /// roles hay en la base.
    /// </summary>
    private static (CreateRole Create, UpdateRole Update, DeleteRole Delete, StubPermissionResolver Resolver)
        Build(ApplicationDbContext context, IEnumerable<string>? callerPermissions = null)
    {
        var repository = new RoleRepository(context);
        var callerRoleId = Guid.NewGuid();
        var resolver = new StubPermissionResolver(context)
            .With(callerRoleId, callerPermissions ?? Permissions.All);
        var caller = new StubCurrentUser(Guid.NewGuid(), callerRoleId);
        var guard = new PermissionEscalationGuard(caller, resolver);

        return (
            new CreateRole(repository, resolver, guard, context),
            new UpdateRole(repository, resolver, guard, context),
            new DeleteRole(repository, resolver, context),
            resolver);
    }

    [Fact]
    public async Task Se_crea_un_rol_con_los_permisos_indicados()
    {
        using var context = TestDbContextFactory.Create();
        var (create, _, _, resolver) = Build(context);

        var result = await create.RunAsync(new CreateRoleRequest(
            "Cajero", "Registra salidas del mostrador",
            [Permissions.Dashboard.View, Permissions.Movements.Create, Permissions.Products.View]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Cajero");
        result.Value.IsSystem.Should().BeFalse("los roles creados nunca son de sistema");
        result.Value.PermissionCount.Should().Be(3);
        // Se devuelven en el orden del catálogo, no en el de entrada.
        result.Value.Permissions.Should().Equal(
            Permissions.Dashboard.View, Permissions.Products.View, Permissions.Movements.Create);
        resolver.InvalidateCalls.Should().Be(1, "la caché del rol nuevo se invalida");
    }

    [Fact]
    public async Task Un_permiso_que_no_esta_en_el_catalogo_se_rechaza()
    {
        using var context = TestDbContextFactory.Create();
        var (create, _, _, _) = Build(context);

        // Guardar una clave inventada daría la impresión de conceder algo que
        // ningún endpoint comprueba.
        var result = await create.RunAsync(new CreateRoleRequest(
            "Inventado", null, [Permissions.Products.View, "productos.superpoder"]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().NotBeNull();
        context.Roles.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_nombre_repetido_se_rechaza()
    {
        using var context = TestDbContextFactory.Create();
        context.Add(TestData.Role("Auditor", isSystem: false, permissions: [Permissions.Reports.View]));
        await context.SaveChangesAsync();

        var (create, _, _, _) = Build(context);

        var result = await create.RunAsync(new CreateRoleRequest("Auditor", null, []));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Se_puede_crear_un_rol_sin_ningun_permiso()
    {
        using var context = TestDbContextFactory.Create();
        var (create, _, _, _) = Build(context);

        // Es un punto de partida legítimo: se marcan los permisos después.
        var result = await create.RunAsync(new CreateRoleRequest("Vacío", null, []));

        result.IsSuccess.Should().BeTrue();
        result.Value.PermissionCount.Should().Be(0);
    }

    [Fact]
    public async Task Editar_reemplaza_el_conjunto_completo_de_permisos()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.Role("Auditor", isSystem: false, permissions:
            [Permissions.Reports.View, Permissions.Products.View, Permissions.Clients.View]);
        context.Add(role);
        await context.SaveChangesAsync();

        var (_, update, _, resolver) = Build(context);

        var result = await update.RunAsync(role.Id, new UpdateRoleRequest(
            "Auditor", "Sólo reportes", [Permissions.Reports.View, Permissions.Reports.Export]));

        result.IsSuccess.Should().BeTrue();
        result.Value.Permissions.Should().Equal(Permissions.Reports.View, Permissions.Reports.Export);
        context.RolePermissions.Count(p => p.RoleId == role.Id).Should().Be(2,
            "los permisos que ya no están se borran, no se acumulan");
        resolver.InvalidateCalls.Should().Be(1, "el cambio surte efecto de inmediato");
    }

    [Fact]
    public async Task El_rol_de_sistema_no_puede_quedarse_sin_permisos_criticos()
    {
        using var context = TestDbContextFactory.Create();
        var system = TestData.Role();      // de sistema, catálogo completo
        context.Add(system);
        await context.SaveChangesAsync();

        var (_, update, _, _) = Build(context);

        var result = await update.RunAsync(system.Id, new UpdateRoleRequest(
            system.Name, system.Description, [Permissions.Dashboard.View]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("sistema");
    }

    [Fact]
    public async Task Quitar_un_permiso_critico_se_bloquea_si_nadie_mas_lo_tiene()
    {
        using var context = TestDbContextFactory.Create();
        // Un rol normal que es el único con capacidad de administrar.
        var admins = TestData.Role("Coordinador", isSystem: false, permissions:
            [Permissions.Roles.Edit, Permissions.Users.Edit, Permissions.Users.View]);
        var user = TestData.User(admins, "coord@test.local");
        context.AddRange(admins, user);
        await context.SaveChangesAsync();

        var (_, update, _, _) = Build(context);

        var result = await update.RunAsync(admins.Id, new UpdateRoleRequest(
            admins.Name, null, [Permissions.Users.View]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain(Permissions.Roles.Edit);
    }

    [Fact]
    public async Task Quitarlo_si_se_permite_cuando_otro_rol_activo_lo_conserva()
    {
        using var context = TestDbContextFactory.Create();
        var system = TestData.Role();
        var systemUser = TestData.User(system, "admin@test.local");
        var other = TestData.Role("Coordinador", isSystem: false, permissions:
            [Permissions.Roles.Edit, Permissions.Users.Edit]);
        var otherUser = TestData.User(other, "coord@test.local");
        context.AddRange(system, systemUser, other, otherUser);
        await context.SaveChangesAsync();

        var (_, update, _, _) = Build(context);

        var result = await update.RunAsync(other.Id, new UpdateRoleRequest(
            other.Name, null, [Permissions.Products.View]));

        result.IsSuccess.Should().BeTrue("el rol de sistema conserva la capacidad");
    }

    [Fact]
    public async Task El_rol_de_sistema_no_se_elimina()
    {
        using var context = TestDbContextFactory.Create();
        var system = TestData.Role();
        context.Add(system);
        await context.SaveChangesAsync();

        var (_, _, delete, _) = Build(context);

        var result = await delete.RunAsync(system.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Roles.Should().HaveCount(1);
    }

    [Fact]
    public async Task Un_rol_con_usuarios_asignados_no_se_elimina()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.OperatorRole();
        context.AddRange(role, TestData.User(role, "juan@test.local"));
        await context.SaveChangesAsync();

        var (_, _, delete, _) = Build(context);

        var result = await delete.RunAsync(role.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("1 usuario");
    }

    [Fact]
    public async Task Un_rol_sin_usuarios_se_elimina_con_sus_permisos()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.Role("Cajero", isSystem: false, permissions:
            [Permissions.Products.View, Permissions.Movements.Create]);
        context.Add(role);
        await context.SaveChangesAsync();

        var (_, _, delete, resolver) = Build(context);

        var result = await delete.RunAsync(role.Id);

        result.IsSuccess.Should().BeTrue();
        context.Roles.Should().BeEmpty();
        resolver.InvalidateCalls.Should().Be(1);
    }

    // ─────────────────────────────────────────── guardia de escalada

    [Fact]
    public async Task No_se_puede_crear_un_rol_con_permisos_que_uno_no_tiene()
    {
        using var context = TestDbContextFactory.Create();
        // Sabe administrar roles, pero no toca usuarios ni borra productos.
        var (create, _, _, _) = Build(context, callerPermissions:
            [Permissions.Roles.View, Permissions.Roles.Create, Permissions.Products.View]);

        var result = await create.RunAsync(new CreateRoleRequest(
            "Títere", null, [Permissions.Products.View, Permissions.Users.Edit]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        result.Error.Message.Should().Contain(Permissions.Users.Edit);
        context.Roles.Should().BeEmpty("el rol no llega a crearse");
    }

    [Fact]
    public async Task Se_puede_crear_un_rol_dentro_de_los_propios_permisos()
    {
        using var context = TestDbContextFactory.Create();
        var (create, _, _, _) = Build(context, callerPermissions:
            [Permissions.Roles.Create, Permissions.Products.View, Permissions.Movements.Create]);

        var result = await create.RunAsync(new CreateRoleRequest(
            "Cajero", null, [Permissions.Products.View, Permissions.Movements.Create]));

        result.IsSuccess.Should().BeTrue("delegar un subconjunto de lo propio es legítimo");
        result.Value.PermissionCount.Should().Be(2);
    }

    [Fact]
    public async Task Nadie_se_concede_a_si_mismo_un_permiso_que_no_tiene()
    {
        using var context = TestDbContextFactory.Create();
        // El escenario que abre el agujero: el rol propio, con roles.edit, se marca
        // el resto de la matriz y sale siendo superusuario.
        var own = TestData.Role("Coordinador", isSystem: false, permissions:
            [Permissions.Roles.View, Permissions.Roles.Edit, Permissions.Users.Edit]);
        context.AddRange(own, TestData.User(own, "coord@test.local"));
        await context.SaveChangesAsync();

        var repository = new RoleRepository(context);
        var resolver = new StubPermissionResolver(context);
        var caller = new StubCurrentUser(Guid.NewGuid(), own.Id);
        var update = new UpdateRole(
            repository, resolver, new PermissionEscalationGuard(caller, resolver), context);

        var result = await update.RunAsync(own.Id, new UpdateRoleRequest(
            own.Name, null, [.. Permissions.All]));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        context.RolePermissions.Count(p => p.RoleId == own.Id).Should().Be(3,
            "el rol se queda exactamente como estaba");
    }

    [Fact]
    public async Task Se_puede_editar_un_rol_mas_poderoso_sin_agregarle_permisos()
    {
        using var context = TestDbContextFactory.Create();
        var powerful = TestData.Role("Auditor", isSystem: false, permissions:
            [Permissions.Reports.View, Permissions.Users.Delete]);
        context.Add(powerful);
        await context.SaveChangesAsync();

        // Quien edita no tiene users.delete, pero tampoco lo está concediendo:
        // sólo cambia el nombre y reenvía la lista tal cual.
        var (_, update, _, _) = Build(context, callerPermissions:
            [Permissions.Roles.View, Permissions.Roles.Edit, Permissions.Reports.View]);

        var result = await update.RunAsync(powerful.Id, new UpdateRoleRequest(
            "Auditor interno", "Renombrado", [Permissions.Reports.View, Permissions.Users.Delete]));

        result.IsSuccess.Should().BeTrue("reenviar lo que ya concedía no es escalada");
        result.Value.Name.Should().Be("Auditor interno");
    }

    [Fact]
    public async Task Quitar_un_permiso_ajeno_tampoco_es_escalada()
    {
        using var context = TestDbContextFactory.Create();
        var system = TestData.Role();
        var other = TestData.Role("Auditor", isSystem: false, permissions:
            [Permissions.Reports.View, Permissions.Products.Delete]);
        context.AddRange(system, TestData.User(system, "admin@test.local"), other);
        await context.SaveChangesAsync();

        var (_, update, _, _) = Build(context, callerPermissions:
            [Permissions.Roles.Edit, Permissions.Reports.View]);

        var result = await update.RunAsync(other.Id, new UpdateRoleRequest(
            other.Name, null, [Permissions.Reports.View]));

        result.IsSuccess.Should().BeTrue("reducir permisos siempre está permitido");
        result.Value.Permissions.Should().Equal(Permissions.Reports.View);
    }

    [Fact]
    public void El_catalogo_expuesto_coincide_con_el_del_codigo()
    {
        var result = new GetPermissionCatalog().Run();

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(Permissions.All.Count);
        result.Value.Permissions.Select(p => p.Key).Should().BeEquivalentTo(Permissions.All);
        result.Value.Modules.Should().HaveCount(
            Permissions.Catalog.Select(d => d.Module).Distinct().Count());
        result.Value.StandardActions.Select(a => a.Action)
            .Should().Equal("view", "create", "edit", "delete");
    }
}
