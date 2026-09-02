using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// El catálogo de permisos vive en el código y los permisos de cada rol son filas.
/// Estas dos cosas se separan solas en cuanto el catálogo cambia; aquí se verifica
/// que el arranque las vuelva a juntar.
/// </summary>
public sealed class PermissionSyncTests
{
    private static (PermissionSynchronizer Sync, StubPermissionResolver Resolver)
        Build(ApplicationDbContext context)
    {
        var resolver = new StubPermissionResolver(context);
        return (
            new PermissionSynchronizer(context, resolver, NullLogger<PermissionSynchronizer>.Instance),
            resolver);
    }

    [Fact]
    public async Task El_rol_de_sistema_recibe_los_permisos_que_le_falten()
    {
        using var context = TestDbContextFactory.Create();
        // El caso real: la migración lo sembró con el catálogo de su día y desde
        // entonces se agregaron claves nuevas al código.
        var system = TestData.Role("Administrador", isSystem: true, permissions:
            [Permissions.Dashboard.View, Permissions.Roles.Edit]);
        context.Add(system);
        await context.SaveChangesAsync();

        var (sync, resolver) = Build(context);
        await sync.SynchronizeAsync();

        context.RolePermissions
            .Where(p => p.RoleId == system.Id)
            .Select(p => p.Permission)
            .Should().BeEquivalentTo(Permissions.All,
                "un rol de sistema es «todo el catálogo», no la foto del día de la migración");
        resolver.InvalidateCalls.Should().Be(1);
    }

    [Fact]
    public async Task Un_rol_normal_no_recibe_permisos_nuevos()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.OperatorRole();
        var before = role.Permissions.Count;
        context.Add(role);
        await context.SaveChangesAsync();

        var (sync, _) = Build(context);
        await sync.SynchronizeAsync();

        context.RolePermissions.Count(p => p.RoleId == role.Id).Should().Be(before,
            "los roles normales los configura quien administra, no el arranque");
    }

    [Fact]
    public async Task Las_claves_que_ya_no_existen_en_el_catalogo_se_borran()
    {
        using var context = TestDbContextFactory.Create();
        var role = TestData.Role("Auditor", isSystem: false, permissions: [Permissions.Reports.View]);
        // Una clave que alguna vez existió y salió del código: no la comprueba
        // ningún [RequirePermission], así que no concede nada, pero la matriz la
        // muestra marcada y hace creer que sí.
        role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = "reports.imprimir" });
        context.Add(role);
        await context.SaveChangesAsync();

        var (sync, resolver) = Build(context);
        await sync.SynchronizeAsync();

        context.RolePermissions
            .Where(p => p.RoleId == role.Id)
            .Select(p => p.Permission)
            .Should().Equal(Permissions.Reports.View);
        resolver.InvalidateCalls.Should().Be(1);
    }

    [Fact]
    public async Task Sincronizar_dos_veces_no_cambia_nada_la_segunda()
    {
        using var context = TestDbContextFactory.Create();
        context.Add(TestData.Role());
        await context.SaveChangesAsync();

        var (sync, resolver) = Build(context);
        await sync.SynchronizeAsync();
        var afterFirst = context.RolePermissions.Count();

        await sync.SynchronizeAsync();

        context.RolePermissions.Count().Should().Be(afterFirst);
        resolver.InvalidateCalls.Should().Be(0,
            "sin cambios no se guarda ni se invalida: es idempotente");
    }

    [Fact]
    public async Task Despues_de_sincronizar_el_rol_de_sistema_puede_conceder_cualquier_cosa()
    {
        using var context = TestDbContextFactory.Create();
        // Cierra el círculo con el guardia de escalada: si el rol de sistema se
        // quedara corto, quien administra no podría conceder los permisos nuevos.
        var system = TestData.Role("Administrador", isSystem: true, permissions: [Permissions.Roles.Edit]);
        context.Add(system);
        await context.SaveChangesAsync();

        var (sync, resolver) = Build(context);
        await sync.SynchronizeAsync();

        var own = await resolver.GetForRoleAsync(system.Id);
        own.Should().BeEquivalentTo(Permissions.All);
    }
}
