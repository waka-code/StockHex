using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;

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

    /// <summary>
    /// Rol con los permisos indicados. Sin argumentos concede todo el catálogo,
    /// que es lo que necesita la mayoría de los tests para no tropezar con los
    /// guardias de acceso crítico.
    /// </summary>
    public static Role Role(
        string name = "Administrador",
        bool isSystem = true,
        IEnumerable<string>? permissions = null)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Rol de prueba: {name}",
            IsSystem = isSystem
        };

        foreach (var key in permissions ?? Permissions.All)
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = key });

        return role;
    }

    /// <summary>Rol de menor privilegio, sin permisos de administración.</summary>
    public static Role OperatorRole() => Role("Bodeguero", isSystem: false, permissions:
    [
        Permissions.Dashboard.View,
        Permissions.Products.View,
        Permissions.Movements.View,
        Permissions.Movements.Create,
        Permissions.Reports.View
    ]);

    public static User User(
        Role? role = null,
        string email = "user@test.local",
        string passwordHash = "hash",
        bool isActive = true)
    {
        role ??= Role();

        return new User
        {
            Id = Guid.NewGuid(),
            Name = "Usuario de prueba",
            Email = email,
            PasswordHash = passwordHash,
            RoleId = role.Id,
            Role = role,
            IsActive = isActive
        };
    }
}
