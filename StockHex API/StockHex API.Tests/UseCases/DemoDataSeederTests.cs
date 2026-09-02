using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Security;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// El sembrador de demostración escribe directo al contexto, sin pasar por
/// <c>CreateMovement</c>. Eso lo hace rápido pero también capaz de producir un
/// historial que no cuadra con el saldo — justo lo que el sistema existe para
/// impedir. Estos tests fijan que la aritmética del libro mayor sea correcta.
/// </summary>
public sealed class DemoDataSeederTests
{
    private static DemoDataSeeder Build(ApplicationDbContext context, bool encendido)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:DemoData"] = encendido ? "true" : "false",
            })
            .Build();

        return new DemoDataSeeder(
            context, new BCryptPasswordHasher(), configuracion,
            NullLogger<DemoDataSeeder>.Instance);
    }

    /// <summary>La migración crea los tres roles base; en memoria hay que ponerlos.</summary>
    private static async Task SembrarRolesBaseAsync(ApplicationDbContext context)
    {
        context.AddRange(
            TestData.Role("Administrador", isSystem: true),
            TestData.Role("Jefe de bodega", isSystem: false, permissions:
            [
                Permissions.Dashboard.View, Permissions.Products.View, Permissions.Products.Create,
                Permissions.Movements.View, Permissions.Movements.Create, Permissions.Reports.View,
            ]),
            TestData.OperatorRole());
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Apagado_no_siembra_absolutamente_nada()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);

        await Build(context, encendido: false).SeedAsync();

        // Es la única protección real contra sembrar una base que no es de juguete.
        context.Products.Should().BeEmpty();
        context.Categories.Should().BeEmpty();
        context.InventoryMovements.Should().BeEmpty();
        context.Users.Should().BeEmpty();
    }

    [Fact]
    public async Task Encendido_llena_todos_los_modulos()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);

        await Build(context, encendido: true).SeedAsync();

        context.Categories.Should().HaveCountGreaterThan(5);
        context.Suppliers.Should().HaveCountGreaterThan(5);
        context.Clients.Should().HaveCountGreaterThan(10);
        context.Products.Should().HaveCountGreaterThan(40);
        context.Users.Should().HaveCountGreaterThan(3);
        context.InventoryMovements.Should().HaveCountGreaterThan(200);
        context.Roles.Should().HaveCountGreaterThan(3, "se añaden roles personalizados");
    }

    [Fact]
    public async Task El_stock_de_cada_producto_es_el_acumulado_de_sus_movimientos()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);
        await Build(context, encendido: true).SeedAsync();

        var productos = await context.Products.AsNoTracking().ToListAsync();
        var movimientos = await context.InventoryMovements.AsNoTracking().ToListAsync();

        foreach (var producto in productos)
        {
            var suyos = movimientos
                .Where(m => m.ProductId == producto.Id)
                .OrderBy(m => m.MovementDate)
                .ToList();

            suyos.Should().NotBeEmpty($"'{producto.Sku}' tiene que tener historial");

            // El primero parte de cero: el producto nace sin existencias.
            suyos[0].StockBefore.Should().Be(0, $"'{producto.Sku}' no puede nacer con stock");

            // Y cada línea arranca donde terminó la anterior.
            for (var i = 1; i < suyos.Count; i++)
            {
                suyos[i].StockBefore.Should().Be(suyos[i - 1].StockAfter,
                    $"la cadena de '{producto.Sku}' se rompe en el movimiento {i}");
            }

            producto.StockQuantity.Should().Be(suyos[^1].StockAfter,
                $"el saldo de '{producto.Sku}' no coincide con su último movimiento");
        }
    }

    [Fact]
    public async Task Ningun_movimiento_deja_el_stock_negativo_ni_contradice_su_tipo()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);
        await Build(context, encendido: true).SeedAsync();

        var movimientos = await context.InventoryMovements.AsNoTracking().ToListAsync();

        movimientos.Should().OnlyContain(m => m.StockAfter >= 0, "el stock nunca queda negativo");

        foreach (var m in movimientos)
        {
            var delta = m.StockAfter - m.StockBefore;
            switch (m.MovementType)
            {
                case MovementType.In:
                    delta.Should().Be(m.Quantity, "una entrada suma exactamente su cantidad");
                    break;
                case MovementType.Out:
                    delta.Should().Be(-m.Quantity, "una salida resta exactamente su cantidad");
                    break;
                case MovementType.Adjustment:
                    m.StockAfter.Should().Be(m.Quantity, "en un ajuste la cantidad es el saldo final");
                    break;
            }
        }
    }

    [Fact]
    public async Task Hay_material_para_ver_cada_pantalla_trabajando()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);
        await Build(context, encendido: true).SeedAsync();

        var productos = await context.Products.AsNoTracking().ToListAsync();
        var movimientos = await context.InventoryMovements.AsNoTracking().ToListAsync();

        // Sin esto el reporte de stock bajo y el aviso del panel salen vacíos.
        productos.Count(p => p.IsActive && p.StockQuantity <= p.MinimumStock)
            .Should().BeGreaterThan(3, "el reporte de stock bajo necesita contenido");

        productos.Count(p => !p.IsActive)
            .Should().BeGreaterThan(0, "el filtro de inactivos necesita contenido");

        movimientos.Count(m => m.ReversalOfMovementId != null)
            .Should().BeGreaterThan(0, "la reversión es la regla que distingue al producto");

        movimientos.Select(m => m.MovementType).Distinct()
            .Should().HaveCount(3, "los tres tipos tienen que aparecer");

        movimientos.Count(m => m.ClientId != null).Should().BeGreaterThan(0);
        movimientos.Count(m => m.SupplierId != null).Should().BeGreaterThan(0);

        // Un movimiento sólo puede revertirse una vez: el índice único filtrado lo
        // impone en SQL Server, y sembrar dos reventaría el arranque.
        movimientos.Where(m => m.ReversalOfMovementId != null)
            .GroupBy(m => m.ReversalOfMovementId)
            .Should().OnlyContain(g => g.Count() == 1);
    }

    [Fact]
    public async Task Sembrar_dos_veces_no_duplica()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);

        var sembrador = Build(context, encendido: true);
        await sembrador.SeedAsync();
        var productos = context.Products.Count();
        var movimientos = context.InventoryMovements.Count();

        await sembrador.SeedAsync();

        context.Products.Count().Should().Be(productos, "la marca de agua evita repetir");
        context.InventoryMovements.Count().Should().Be(movimientos);
    }

    [Fact]
    public async Task Los_usuarios_sembrados_pueden_entrar_con_la_contrasena_documentada()
    {
        using var context = TestDbContextFactory.Create();
        await SembrarRolesBaseAsync(context);
        await Build(context, encendido: true).SeedAsync();

        var hasher = new BCryptPasswordHasher();
        var usuarios = await context.Users.AsNoTracking().ToListAsync();

        // Si la contraseña documentada no sirve, la demo no se puede recorrer.
        usuarios.Should().OnlyContain(u => hasher.Verify(DemoDataSeeder.DemoPassword, u.PasswordHash));
        usuarios.Should().Contain(u => !u.IsActive, "hace falta uno desactivado para el filtro");
    }
}
