using FluentAssertions;
using StockHex_API.Application.UseCases.CategoryUseCases;
using StockHex_API.Application.UseCases.ProductUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>Verifica que los borrados no puedan romper la integridad ni perder auditoría.</summary>
public sealed class DeleteEntityGuardTests
{
    [Fact]
    public async Task No_se_puede_eliminar_una_categoria_con_productos()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        context.AddRange(category, TestData.Product(category.Id));
        await context.SaveChangesAsync();

        var useCase = new DeleteCategory(new CategoryRepository(context), context);

        var result = await useCase.RunAsync(category.Id);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        context.Categories.Should().HaveCount(1);
    }

    [Fact]
    public async Task Una_categoria_sin_productos_se_elimina()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        context.Add(category);
        await context.SaveChangesAsync();

        var result = await new DeleteCategory(new CategoryRepository(context), context)
            .RunAsync(category.Id);

        result.IsSuccess.Should().BeTrue();
        context.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_producto_con_movimientos_se_desactiva_en_lugar_de_borrarse()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id, stock: 5);
        var user = TestData.User();
        context.AddRange(category, product, user);
        context.InventoryMovements.Add(new StockHex_API.Domain.Entities.InventoryMovement
        {
            ProductId = product.Id,
            UserId = user.Id,
            MovementType = MovementType.In,
            Quantity = 5,
            StockAfter = 5
        });
        await context.SaveChangesAsync();

        var useCase = new DeleteProduct(
            new ProductRepository(context),
            new InventoryMovementRepository(context),
            context);

        var result = await useCase.RunAsync(product.Id);

        // Devuelve conflicto porque no se borró, e informa que quedó desactivado.
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Contain("desactivó");

        context.Products.Should().HaveCount(1);
        context.Products.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Un_producto_sin_movimientos_se_elimina()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id);
        context.AddRange(category, product);
        await context.SaveChangesAsync();

        var result = await new DeleteProduct(
            new ProductRepository(context),
            new InventoryMovementRepository(context),
            context).RunAsync(product.Id);

        result.IsSuccess.Should().BeTrue();
        context.Products.Should().BeEmpty();
    }
}
