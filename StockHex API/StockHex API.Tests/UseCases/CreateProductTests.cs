using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.ProductUseCases;
using StockHex_API.Domain.Common;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

public sealed class CreateProductTests
{
    private static CreateProduct BuildUseCase(ApplicationDbContext context) =>
        new(
            new ProductRepository(context),
            new CategoryRepository(context),
            new SupplierRepository(context),
            context);

    [Fact]
    public async Task El_producto_se_crea_con_stock_en_cero()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        context.Add(category);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context).RunAsync(
            new CreateProductRequest("Coca Cola 1L", null, "coca-1l", 1500m, 10, category.Id, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.StockQuantity.Should().Be(0);
        // El SKU se normaliza a mayúsculas para que la unicidad no dependa del formato.
        result.Value.Sku.Should().Be("COCA-1L");
        result.Value.CategoryName.Should().Be(category.Name);
    }

    [Fact]
    public async Task Sku_duplicado_devuelve_conflicto()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        context.AddRange(category, TestData.Product(category.Id, sku: "SKU-DUP"));
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context).RunAsync(
            new CreateProductRequest("Otro", null, "sku-dup", 100m, 0, category.Id, null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task Categoria_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();

        var result = await BuildUseCase(context).RunAsync(
            new CreateProductRequest("Producto", null, "SKU-X", 100m, 0, Guid.NewGuid(), null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain("Categoría");
    }

    [Fact]
    public async Task Proveedor_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        context.Add(category);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context).RunAsync(
            new CreateProductRequest("Producto", null, "SKU-X", 100m, 0, category.Id, Guid.NewGuid()));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain("Proveedor");
    }
}
