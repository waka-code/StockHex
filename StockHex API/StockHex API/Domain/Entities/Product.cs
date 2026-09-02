namespace StockHex_API.Domain.Entities;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Código único de inventario.</summary>
    public string Sku { get; set; } = string.Empty;

    public decimal Price { get; set; }

    /// <summary>Sólo lo modifican los movimientos de inventario, nunca un update directo del producto.</summary>
    public int StockQuantity { get; set; }

    /// <summary>Umbral para el reporte de stock bajo.</summary>
    public int MinimumStock { get; set; }

    public bool IsActive { get; set; } = true;

    public Guid CategoryId { get; set; }

    public Category? Category { get; set; }

    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Token de concurrencia: evita que dos movimientos simultáneos pisen el mismo stock.</summary>
    public byte[]? RowVersion { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();

    /// <summary>True cuando el stock cayó al umbral configurado o por debajo.</summary>
    public bool IsLowStock => StockQuantity <= MinimumStock;
}
