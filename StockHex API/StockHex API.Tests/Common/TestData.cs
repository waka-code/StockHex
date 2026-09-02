using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Tests.Common;

/// <summary>Constructores de entidades con valores válidos por defecto, sobreescribibles por test.</summary>
internal static class TestData
{
    public static Category Category(string name = "Bebidas") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = "Categoría de prueba"
    };

    public static Supplier Supplier(string name = "Proveedor Uno") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = "proveedor@test.local"
    };

    public static Client Client(string name = "Cliente Uno") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = "cliente@test.local"
    };

    public static Product Product(
        Guid categoryId,
        string sku = "SKU-001",
        int stock = 10,
        int minimumStock = 5,
        bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Producto de prueba",
        Sku = sku,
        Price = 100m,
        StockQuantity = stock,
        MinimumStock = minimumStock,
        IsActive = isActive,
        CategoryId = categoryId
    };

    public static User User(
        UserRole role = UserRole.Operator,
        string email = "user@test.local",
        string passwordHash = "hash",
        bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Usuario de prueba",
        Email = email,
        PasswordHash = passwordHash,
        Role = role,
        IsActive = isActive
    };
}
