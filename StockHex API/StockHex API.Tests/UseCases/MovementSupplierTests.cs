using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.UseCases.InventoryMovementUseCases;
using StockHex_API.Application.Validators;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Tests.Common;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Sin proveedor en el movimiento, una entrada no dejaba constancia de a quién se
/// le compró, y no había forma de reconstruir el costo por proveedor.
/// </summary>
public sealed class MovementSupplierTests
{
    private static CreateMovement BuildUseCase(ApplicationDbContext context, Guid userId) =>
        new(
            new InventoryMovementRepository(context),
            new ProductRepository(context),
            new ClientRepository(context),
            new SupplierRepository(context),
            new UserRepository(context),
            new StubCurrentUser(userId),
            context);

    [Fact]
    public async Task Una_entrada_registra_el_proveedor_indicado()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var supplier = TestData.Supplier();
        var product = TestData.Product(category.Id);
        var user = TestData.User();
        context.AddRange(category, supplier, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, 250m, null, supplier.Id, "Compra"));

        result.IsSuccess.Should().BeTrue();
        result.Value.SupplierId.Should().Be(supplier.Id);
        result.Value.SupplierName.Should().Be(supplier.Name);
    }

    [Fact]
    public async Task Una_entrada_sin_proveedor_hereda_el_del_producto()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var supplier = TestData.Supplier();
        var product = TestData.Product(category.Id);
        product.SupplierId = supplier.Id;
        var user = TestData.User();
        context.AddRange(category, supplier, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, null, null, null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.SupplierId.Should().Be(supplier.Id, "es el caso habitual y no se debe perder el dato");
    }

    [Fact]
    public async Task Un_proveedor_inexistente_devuelve_no_encontrado()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var product = TestData.Product(category.Id);
        var user = TestData.User();
        context.AddRange(category, product, user);
        await context.SaveChangesAsync();

        var result = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 10, null, null, Guid.NewGuid(), null));

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Message.Should().Contain("Proveedor");
    }

    // ---------------------------------------------------------------- Invariante

    [Fact]
    public void El_validador_rechaza_cliente_y_proveedor_a_la_vez()
    {
        var validator = new CreateMovementRequestValidator();

        // Un movimiento tiene una sola contraparte.
        var result = validator.Validate(new CreateMovementRequest(
            Guid.NewGuid(), MovementType.Out, 5, null, Guid.NewGuid(), Guid.NewGuid(), null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMovementRequest.ClientId));
    }

    [Theory]
    [InlineData(MovementType.In)]
    [InlineData(MovementType.Out)]
    [InlineData(MovementType.Adjustment)]
    public void El_proveedor_se_admite_en_cualquier_tipo(MovementType type)
    {
        // La regla no se ata al tipo: una devolución a proveedor es una salida con
        // proveedor. Atarla al tipo hacía que la reversión de una compra generara un
        // estado que este mismo endpoint rechazaba.
        var validator = new CreateMovementRequestValidator();

        var result = validator.Validate(new CreateMovementRequest(
            Guid.NewGuid(), type, 5, null, null, Guid.NewGuid(), null));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(MovementType.In)]
    [InlineData(MovementType.Out)]
    public void El_cliente_se_admite_en_cualquier_tipo(MovementType type)
    {
        // Simétrico: una devolución de cliente es una entrada con cliente.
        var validator = new CreateMovementRequestValidator();

        var result = validator.Validate(new CreateMovementRequest(
            Guid.NewGuid(), type, 5, null, Guid.NewGuid(), null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_reversion_de_una_compra_produce_un_estado_que_el_endpoint_acepta()
    {
        using var context = TestDbContextFactory.Create();
        var category = TestData.Category();
        var supplier = TestData.Supplier();
        var product = TestData.Product(category.Id, stock: 0);
        var user = TestData.User();
        context.AddRange(category, supplier, product, user);
        await context.SaveChangesAsync();

        var entrada = await BuildUseCase(context, user.Id).RunAsync(
            new CreateMovementRequest(product.Id, MovementType.In, 5, 100m, null, supplier.Id, null));

        var reversal = await new ReverseMovement(
            new InventoryMovementRepository(context),
            new ProductRepository(context),
            new UserRepository(context),
            new StubCurrentUser(user.Id),
            context).RunAsync(entrada.Value.Id, new ReverseMovementRequest(null));

        reversal.IsSuccess.Should().BeTrue();
        reversal.Value.MovementType.Should().Be(MovementType.Out);
        reversal.Value.SupplierId.Should().Be(supplier.Id, "se conserva la contraparte");

        // El mismo estado por la puerta de entrada normal también es válido: antes
        // el validador lo rechazaba con 400 y las dos rutas discrepaban.
        new CreateMovementRequestValidator().Validate(new CreateMovementRequest(
                product.Id, MovementType.Out, 5, null, null, supplier.Id, null))
            .IsValid.Should().BeTrue();
    }
}
