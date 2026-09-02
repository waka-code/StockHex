using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.InventoryMovementUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// La reversión invierte la variación neta del movimiento, no su cantidad. Eso la
/// hace correcta para entradas, salidas y ajustes por igual.
/// </summary>
public sealed class ReverseMovementTests
{
    private static CreateMovement BuildCreate(ApplicationDbContext context, Guid userId) =>
        new(
            new InventoryMovementRepository(context),
            new ProductRepository(context),
            new ClientRepository(context),
            new SupplierRepository(context),
            new UserRepository(context),
            new StubCurrentUser(userId),
            context);

    private static ReverseMovement BuildReverse(ApplicationDbContext context, Guid? userId) =>
        new(
            new InventoryMovementRepository(context),
            new ProductRepository(context),
            new UserRepository(context),
            new StubCurrentUser(userId),
            context);

    private static async Task<(Product Product, User User)> SeedAsync(
        ApplicationDbContext context,
        int stock = 0)
    {
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: stock);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();
        return (product, user);
    }

    [Fact]
    public async Task Revertir_una_entrada_resta_las_unidades_y_registra_una_salida()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);

        var entrada = await BuildCreate(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 50, null, null, null, "Compra"));
        entrada.IsSuccess.Should().BeTrue();

        var result = await BuildReverse(context, user.Id).RunAsync(
            entrada.Value.Id, new ReverseMovementRequest("Factura anulada"));

        result.IsSuccess.Should().BeTrue();
        result.Value.MovementType.Should().Be(MovementType.Out);
        result.Value.Quantity.Should().Be(50);
        result.Value.StockAfter.Should().Be(0);
        result.Value.ReversalOfMovementId.Should().Be(entrada.Value.Id);
        result.Value.Comment.Should().Contain("Reversión").And.Contain("Factura anulada");

        context.Products.Single().StockQuantity.Should().Be(0);
        // El original sigue intacto: el historial no se reescribe.
        context.InventoryMovements.Should().HaveCount(2);
    }

    [Fact]
    public async Task Revertir_una_salida_devuelve_las_unidades_al_stock()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context, stock: 0);
        var create = BuildCreate(context, user.Id);

        await create.RunAsync(new CreateMovementRequest(product.Id, MovementType.In, 100, null, null, null, null));
        var salida = await create.RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Out, 40, null, null, null, "Venta"));

        var result = await BuildReverse(context, user.Id).RunAsync(
            salida.Value.Id, new ReverseMovementRequest(null));

        result.IsSuccess.Should().BeTrue();
        result.Value.MovementType.Should().Be(MovementType.In);
        result.Value.StockAfter.Should().Be(100);
        context.Products.Single().StockQuantity.Should().Be(100);
    }

    [Fact]
    public async Task Revertir_un_ajuste_deshace_exactamente_su_variacion()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);
        var create = BuildCreate(context, user.Id);

        await create.RunAsync(new CreateMovementRequest(product.Id, MovementType.In, 30, null, null, null, null));
        // Ajuste de 30 a 12: variación de -18.
        var ajuste = await create.RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Adjustment, 12, null, null, null, "Conteo"));
        ajuste.Value.StockAfter.Should().Be(12);

        var result = await BuildReverse(context, user.Id).RunAsync(
            ajuste.Value.Id, new ReverseMovementRequest("Conteo equivocado"));

        result.IsSuccess.Should().BeTrue();
        result.Value.MovementType.Should().Be(MovementType.In);
        result.Value.Quantity.Should().Be(18);
        result.Value.StockAfter.Should().Be(30);
    }

    [Fact]
    public async Task Revertir_sigue_siendo_exacto_si_hubo_movimientos_posteriores()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);
        var create = BuildCreate(context, user.Id);

        var entrada = await create.RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 100, null, null, null, null));
        // Actividad posterior: la reversión no debe restaurar un stock histórico,
        // sólo descontar las 100 unidades que aportó el movimiento revertido.
        await create.RunAsync(new CreateMovementRequest(product.Id, MovementType.In, 20, null, null, null, null));
        await create.RunAsync(new CreateMovementRequest(product.Id, MovementType.Out, 5, null, null, null, null));
        context.Products.Single().StockQuantity.Should().Be(115);

        var result = await BuildReverse(context, user.Id).RunAsync(
            entrada.Value.Id, new ReverseMovementRequest(null));

        result.IsSuccess.Should().BeTrue();
        result.Value.StockAfter.Should().Be(15, "115 - 100");
    }

    [Fact]
    public async Task Un_movimiento_no_se_puede_revertir_dos_veces()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);

        var entrada = await BuildCreate(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, null, null, null, null));

        var reverse = BuildReverse(context, user.Id);
        (await reverse.RunAsync(entrada.Value.Id, new ReverseMovementRequest(null)))
            .IsSuccess.Should().BeTrue();

        var second = await reverse.RunAsync(entrada.Value.Id, new ReverseMovementRequest(null));

        second.IsFailure.Should().BeTrue();
        second.Error!.Type.Should().Be(ErrorType.Conflict);
        second.Error.Message.Should().Contain("ya fue revertido");
        context.Products.Single().StockQuantity.Should().Be(0, "el stock no se tocó de nuevo");
    }

    [Fact]
    public async Task No_se_puede_revertir_una_reversion()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);

        var entrada = await BuildCreate(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, null, null, null, null));

        var reverse = BuildReverse(context, user.Id);
        var reversal = await reverse.RunAsync(entrada.Value.Id, new ReverseMovementRequest(null));

        var result = await reverse.RunAsync(reversal.Value.Id, new ReverseMovementRequest(null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("reversión");
    }

    [Fact]
    public async Task No_se_revierte_si_el_stock_actual_no_alcanza()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);
        var create = BuildCreate(context, user.Id);

        var entrada = await create.RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 100, null, null, null, null));
        // Ya se vendió casi todo: no quedan 100 unidades que devolver.
        await create.RunAsync(new CreateMovementRequest(product.Id, MovementType.Out, 95, null, null, null, null));

        var result = await BuildReverse(context, user.Id).RunAsync(
            entrada.Value.Id, new ReverseMovementRequest(null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Products.Single().StockQuantity.Should().Be(5, "nada cambió");
    }

    [Fact]
    public async Task Un_movimiento_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();
        var (_, user) = await SeedAsync(context);

        var result = await BuildReverse(context, user.Id).RunAsync(
            Guid.NewGuid(), new ReverseMovementRequest(null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Sin_usuario_autenticado_no_se_puede_revertir()
    {
        using var context = TestDbContextFactory.Create();
        var (product, user) = await SeedAsync(context);

        var entrada = await BuildCreate(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, null, null, null, null));

        var result = await BuildReverse(context, userId: null).RunAsync(
            entrada.Value.Id, new ReverseMovementRequest(null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task La_reversion_se_atribuye_a_quien_la_hace_no_al_autor_original()
    {
        using var context = TestDbContextFactory.Create();
        var (product, author) = await SeedAsync(context);
        var corrector = TestData.User(email: "corrector@test.local");
        context.Add(corrector);
        await context.SaveChangesAsync();

        var entrada = await BuildCreate(context, author.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, null, null, null, null));

        var result = await BuildReverse(context, corrector.Id).RunAsync(
            entrada.Value.Id, new ReverseMovementRequest(null));

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(corrector.Id);
    }
}
