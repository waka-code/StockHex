using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.InventoryMovementUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Cubre la regla central del sistema: cómo cada tipo de movimiento afecta al stock
/// y qué situaciones deben rechazarse.
/// </summary>
public sealed class CreateMovementTests
{
    private static CreateMovement BuildUseCase(ApplicationDbContext context, Guid? currentUserId) =>
        new(
            new InventoryMovementRepository(context),
            new ProductRepository(context),
            new ClientRepository(context),
            new SupplierRepository(context),
            new UserRepository(context),
            new StubCurrentUser(currentUserId),
            context);

    [Fact]
    public async Task Entrada_suma_al_stock_y_registra_el_stock_resultante()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 10);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var useCase = BuildUseCase(context, user.Id);

        var result = await useCase.RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 15, 50m, null, null, "Compra"));

        result.IsSuccess.Should().BeTrue();
        result.Value.StockAfter.Should().Be(25);
        result.Value.UserId.Should().Be(user.Id);

        context.Products.Single().StockQuantity.Should().Be(25);
        context.InventoryMovements.Should().HaveCount(1);
    }

    [Fact]
    public async Task Salida_resta_del_stock()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 10);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Out, 4, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.StockAfter.Should().Be(6);
        context.Products.Single().StockQuantity.Should().Be(6);
    }

    [Fact]
    public async Task Salida_mayor_al_stock_se_rechaza_y_no_altera_nada()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 3);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Out, 4, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("Stock insuficiente");

        // Ni el stock ni el historial deben haberse tocado.
        context.Products.Single().StockQuantity.Should().Be(3);
        context.InventoryMovements.Should().BeEmpty();
    }

    [Fact]
    public async Task Salida_que_deja_el_stock_en_cero_se_permite()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 5);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Out, 5, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.StockAfter.Should().Be(0);
    }

    [Fact]
    public async Task Ajuste_fija_el_stock_al_valor_indicado_sin_sumar_ni_restar()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 10);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Adjustment, 7, null, null, null, "Conteo físico"));

        result.IsSuccess.Should().BeTrue();
        result.Value.StockAfter.Should().Be(7);
        context.Products.Single().StockQuantity.Should().Be(7);
    }

    [Fact]
    public async Task Producto_desactivado_no_admite_movimientos()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 10, isActive: false);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 1, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("desactivado");
    }

    [Fact]
    public async Task Producto_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();
        var user = TestData.User();
        context.Add(user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(Guid.NewGuid(), MovementType.In, 1, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Sin_usuario_autenticado_se_rechaza()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id);
        context.AddRange(category, product);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, currentUserId: null).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 1, null, null, null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task Cliente_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 10);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.Out, 1, null, Guid.NewGuid(), null, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain("Cliente");
    }

    [Fact]
    public async Task Movimientos_sucesivos_acumulan_el_stock_correctamente()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 0);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var useCase = BuildUseCase(context, user.Id);

        await useCase.RunAsync(new CreateMovementRequest(product.Id, MovementType.In, 100, null, null, null, null));
        await useCase.RunAsync(new CreateMovementRequest(product.Id, MovementType.Out, 30, null, null, null, null));
        await useCase.RunAsync(new CreateMovementRequest(product.Id, MovementType.In, 5, null, null, null, null));

        context.Products.Single().StockQuantity.Should().Be(75);
        context.InventoryMovements.Should().HaveCount(3);
        context.InventoryMovements.OrderBy(m => m.MovementDate)
            .Select(m => m.StockAfter)
            .Should().Equal(100, 70, 75);
    }
}
