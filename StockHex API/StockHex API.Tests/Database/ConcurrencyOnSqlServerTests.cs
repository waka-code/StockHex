using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.InventoryMovementUseCases;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.Database;

/// <summary>
/// El token de concurrencia de <c>Product</c> y el reintento que lo acompaña.
///
/// <c>RowVersion</c> es una columna <c>rowversion</c> que <b>genera el motor</b>: el
/// proveedor InMemory ni la mantiene ni detecta el conflicto, así que
/// <c>ConcurrencyRetryTests</c> puede verificar la política de reintento pero no
/// que exista el conflicto que la justifica. Eso se prueba aquí.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConcurrencyOnSqlServerTests
{
    private readonly SqlServerFixture _sql;

    public ConcurrencyOnSqlServerTests(SqlServerFixture sql) => _sql = sql;

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    [RequiresDockerFact]
    public async Task Dos_escrituras_simultaneas_sobre_el_mismo_producto_chocan()
    {
        var productId = await SeedProductAsync(stock: 10);

        // Dos contextos que leyeron la misma fila: la segunda escritura va con un
        // RowVersion que el motor ya cambió.
        await using var primero = _sql.CreateContext();
        await using var segundo = _sql.CreateContext();

        var enPrimero = await primero.Products.FirstAsync(p => p.Id == productId);
        var enSegundo = await segundo.Products.FirstAsync(p => p.Id == productId);

        enPrimero.StockQuantity = 20;
        await primero.SaveChangesAsync();

        enSegundo.StockQuantity = 30;

        var act = () => segundo.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "sin esto la segunda escritura pisaría la primera y se perdería stock");
    }

    [RequiresDockerFact]
    public async Task El_rowversion_cambia_en_cada_actualizacion()
    {
        var productId = await SeedProductAsync(stock: 5);

        await using var context = _sql.CreateContext();
        var product = await context.Products.FirstAsync(p => p.Id == productId);
        var inicial = product.RowVersion;

        inicial.Should().NotBeNull("lo genera el motor al insertar");

        product.StockQuantity = 6;
        await context.SaveChangesAsync();

        product.RowVersion.Should().NotBeEquivalentTo(inicial);
    }

    [RequiresDockerFact]
    public async Task Movimientos_en_paralelo_no_pierden_ninguna_unidad()
    {
        const int enParalelo = 25;
        var productId = await SeedProductAsync(stock: 0);
        var userId = await SeedUserAsync();

        // Es la afirmación del README medida como test: 25 entradas simultáneas
        // sobre el mismo producto terminan en 25 éxitos y el stock exacto. El
        // RowVersion provoca los conflictos; ExecuteWithConcurrencyRetryAsync los
        // reintenta releyendo, y ninguna unidad se pierde por el camino.
        var resultados = await Task.WhenAll(Enumerable.Range(0, enParalelo).Select(async _ =>
        {
            // Un contexto por tarea: DbContext no es seguro entre hilos.
            await using var context = _sql.CreateContext();

            var useCase = new CreateMovement(
                new InventoryMovementRepository(context),
                new ProductRepository(context),
                new ClientRepository(context),
                new SupplierRepository(context),
                new UserRepository(context),
                new StubCurrentUser(userId),
                context);

            return await useCase.RunAsync(
                new CreateMovementRequest(productId, MovementType.In, 1, null, null, null, null));
        }));

        resultados.Should().OnlyContain(r => r.IsSuccess,
            "el reintento existe para que la protección no se traduzca en rechazos");

        await using var verificacion = _sql.CreateContext();
        var producto = await verificacion.Products.AsNoTracking().FirstAsync(p => p.Id == productId);

        producto.StockQuantity.Should().Be(enParalelo);
        verificacion.InventoryMovements.Count(m => m.ProductId == productId).Should().Be(enParalelo);
    }

    [RequiresDockerFact]
    public async Task Salidas_en_paralelo_no_dejan_el_stock_negativo()
    {
        const int disponible = 10;
        const int intentos = 20;
        var productId = await SeedProductAsync(stock: disponible);
        var userId = await SeedUserAsync();

        // Se piden más unidades de las que hay. Lo importante no es cuántas pasan,
        // sino que el stock no quede negativo: sin el token de concurrencia, dos
        // salidas que leen el mismo stock lo dejan por debajo de cero.
        var resultados = await Task.WhenAll(Enumerable.Range(0, intentos).Select(async _ =>
        {
            await using var context = _sql.CreateContext();

            var useCase = new CreateMovement(
                new InventoryMovementRepository(context),
                new ProductRepository(context),
                new ClientRepository(context),
                new SupplierRepository(context),
                new UserRepository(context),
                new StubCurrentUser(userId),
                context);

            return await useCase.RunAsync(
                new CreateMovementRequest(productId, MovementType.Out, 1, null, null, null, null));
        }));

        resultados.Count(r => r.IsSuccess).Should().Be(disponible,
            "sólo hay diez unidades: el resto tiene que fallar por stock insuficiente");

        await using var verificacion = _sql.CreateContext();
        var producto = await verificacion.Products.AsNoTracking().FirstAsync(p => p.Id == productId);

        producto.StockQuantity.Should().Be(0);
    }

    // ───────────────────────────────────────────────────── helpers

    private async Task<Guid> SeedProductAsync(int stock)
    {
        await using var context = _sql.CreateContext();
        var category = TestData.Category($"Cat {Unique()}");
        context.Add(category);
        await context.SaveChangesAsync();

        var product = TestData.Product(category.Id, $"SKU-{Unique()}", stock: stock, minimumStock: 0);
        context.Add(product);
        await context.SaveChangesAsync();

        return product.Id;
    }

    private async Task<Guid> SeedUserAsync()
    {
        await using var context = _sql.CreateContext();
        var role = TestData.Role($"Rol {Unique()}", isSystem: false,
            permissions: [Permissions.Movements.Create]);
        var user = TestData.User(role, $"user-{Unique()}@test.local");
        context.AddRange(role, user);
        await context.SaveChangesAsync();

        return user.Id;
    }
}
