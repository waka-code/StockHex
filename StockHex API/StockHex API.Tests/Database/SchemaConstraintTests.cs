using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.Database;

/// <summary>
/// Las garantías que el diseño delega en la base de datos. El proveedor InMemory
/// no aplica ninguna de éstas, así que sin estos tests el resto de la suite pasaba
/// en verde mientras el esquema real podía estar diciendo otra cosa.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class SchemaConstraintTests
{
    private readonly SqlServerFixture _sql;

    public SchemaConstraintTests(SqlServerFixture sql) => _sql = sql;

    /// <summary>Sufijo único: los tests de la colección comparten una sola base.</summary>
    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    // ───────────────────────────────────────────────── migraciones

    [RequiresDockerFact]
    public async Task Las_migraciones_se_aplican_y_no_queda_ninguna_pendiente()
    {
        await using var context = _sql.CreateContext();

        // El fixture ya migró. Si el snapshot y las migraciones se hubieran
        // separado, aquí aparecería la diferencia.
        (await context.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await context.Database.GetAppliedMigrationsAsync()).Should().NotBeEmpty();
    }

    // ───────────────────────────────────────────────── índices únicos

    [RequiresDockerFact]
    public async Task Dos_productos_no_pueden_compartir_el_SKU()
    {
        await using var context = _sql.CreateContext();
        var category = TestData.Category($"Cat {Unique()}");
        context.Add(category);
        await context.SaveChangesAsync();

        var sku = $"SKU-{Unique()}";
        context.Add(TestData.Product(category.Id, sku));
        await context.SaveChangesAsync();

        context.Add(TestData.Product(category.Id, sku));

        // La comprobación previa del caso de uso puede perder la carrera; el índice
        // único es lo que impide de verdad el duplicado.
        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [RequiresDockerFact]
    public async Task El_SKU_distingue_mayusculas_de_minusculas()
    {
        await using var context = _sql.CreateContext();
        var category = TestData.Category($"Cat {Unique()}");
        context.Add(category);
        await context.SaveChangesAsync();

        var raiz = Unique();
        context.Add(TestData.Product(category.Id, $"sku-{raiz}"));
        context.Add(TestData.Product(category.Id, $"SKU-{raiz}"));

        // La columna lleva colación CS_AS a propósito: 'ab-1' y 'AB-1' son códigos
        // distintos. Con la colación por defecto de SQL Server, que no distingue,
        // el segundo insert chocaría contra el índice único.
        var act = () => context.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [RequiresDockerFact]
    public async Task Dos_roles_no_pueden_llamarse_igual()
    {
        await using var context = _sql.CreateContext();
        var nombre = $"Rol {Unique()}";
        context.Add(TestData.Role(nombre, isSystem: false, permissions: [Permissions.Reports.View]));
        await context.SaveChangesAsync();

        context.Add(TestData.Role(nombre, isSystem: false, permissions: [Permissions.Products.View]));

        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [RequiresDockerFact]
    public async Task Un_rol_no_concede_el_mismo_permiso_dos_veces()
    {
        await using var context = _sql.CreateContext();
        var role = TestData.Role($"Rol {Unique()}", isSystem: false, permissions: [Permissions.Reports.View]);
        context.Add(role);
        await context.SaveChangesAsync();

        context.Add(new RolePermission { RoleId = role.Id, Permission = Permissions.Reports.View });

        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ───────────────────────────────────── índice único filtrado

    [RequiresDockerFact]
    public async Task Un_movimiento_no_se_puede_revertir_dos_veces()
    {
        await using var context = _sql.CreateContext();
        var (_, product, user) = await SeedProductAndUserAsync(context);

        var original = Movement(product.Id, user.Id, MovementType.In, 10, before: 0, after: 10);
        context.Add(original);
        await context.SaveChangesAsync();

        context.Add(Reversal(original, product.Id, user.Id));
        await context.SaveChangesAsync();

        // Una segunda reversión del mismo movimiento: la comprobación previa del
        // caso de uso ya la rechaza, pero la garantía dura es el índice filtrado.
        context.Add(Reversal(original, product.Id, user.Id));

        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [RequiresDockerFact]
    public async Task Muchos_movimientos_sin_reversion_conviven_sin_chocar()
    {
        await using var context = _sql.CreateContext();
        var (_, product, user) = await SeedProductAndUserAsync(context);

        // El índice es único pero filtrado por 'IS NOT NULL'. Sin el filtro, el
        // segundo movimiento normal chocaría contra el primero: en SQL Server un
        // índice único admite un solo NULL.
        for (var i = 0; i < 5; i++)
            context.Add(Movement(product.Id, user.Id, MovementType.In, 1, before: i, after: i + 1));

        var act = () => context.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    // ───────────────────────────────────────────── borrado y auditoría

    [RequiresDockerFact]
    public async Task No_se_puede_borrar_una_categoria_con_productos()
    {
        await using var context = _sql.CreateContext();
        var category = TestData.Category($"Cat {Unique()}");
        context.Add(category);
        await context.SaveChangesAsync();
        context.Add(TestData.Product(category.Id, $"SKU-{Unique()}"));
        await context.SaveChangesAsync();

        // ExecuteDelete va directo a SQL. Con Remove() el rastreador de EF cortaría
        // antes con un InvalidOperationException por la relación requerida, y el
        // test estaría verificando el cliente en vez de la restricción de la base,
        // que es lo único que protege cuando el borrado llega por otra vía.
        var act = () => context.Categories.Where(c => c.Id == category.Id).ExecuteDeleteAsync();

        // DeleteBehavior.Restrict: borrar en cascada se llevaría el historial.
        // ExecuteDelete no pasa por el pipeline de actualización de EF, así que el
        // error llega crudo del motor y no envuelto en DbUpdateException.
        (await act.Should().ThrowAsync<SqlException>())
            .Which.Message.Should().Contain("FK_Products_Categories_CategoryId");

        context.Categories.AsNoTracking().Count(c => c.Id == category.Id).Should().Be(1);
    }

    [RequiresDockerFact]
    public async Task Borrar_un_usuario_se_lleva_sus_refresh_tokens()
    {
        await using var context = _sql.CreateContext();
        var role = TestData.Role($"Rol {Unique()}", isSystem: false, permissions: [Permissions.Products.View]);
        var user = TestData.User(role, $"user-{Unique()}@test.local");
        context.AddRange(role, user);
        context.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = $"hash-{Unique()}",
            ExpiresAt = DateTime.UtcNow.AddDays(14),
        });
        await context.SaveChangesAsync();

        context.Remove(user);
        await context.SaveChangesAsync();

        // Cascade sólo aquí: los tokens de un usuario borrado no son auditoría.
        context.RefreshTokens.Count(t => t.UserId == user.Id).Should().Be(0);
    }

    // ───────────────────────────────────────────────────── helpers

    private async Task<(Category Category, Product Product, User User)>
        SeedProductAndUserAsync(Infrastructure.Persistence.ApplicationDbContext context)
    {
        var category = TestData.Category($"Cat {Unique()}");
        var role = TestData.Role($"Rol {Unique()}", isSystem: false, permissions: [Permissions.Movements.Create]);
        var user = TestData.User(role, $"user-{Unique()}@test.local");
        context.AddRange(category, role, user);
        await context.SaveChangesAsync();

        var product = TestData.Product(category.Id, $"SKU-{Unique()}", stock: 0);
        context.Add(product);
        await context.SaveChangesAsync();

        return (category, product, user);
    }

    private static InventoryMovement Movement(
        Guid productId, Guid userId, MovementType type, int quantity, int before, int after) => new()
        {
            ProductId = productId,
            UserId = userId,
            MovementType = type,
            Quantity = quantity,
            StockBefore = before,
            StockAfter = after,
            MovementDate = DateTime.UtcNow,
        };

    private static InventoryMovement Reversal(InventoryMovement original, Guid productId, Guid userId)
    {
        var reversal = Movement(productId, userId, MovementType.Out, original.Quantity,
            before: original.StockAfter, after: original.StockBefore);
        reversal.ReversalOfMovementId = original.Id;
        return reversal;
    }
}
