using StockHex_API.Domain.Enums;

namespace StockHex_API.Domain.Entities;

/// <summary>
/// Registro inmutable de un cambio de stock. Es la fuente de verdad del inventario:
/// <see cref="Product.StockQuantity"/> es el acumulado de estos movimientos.
/// </summary>
public class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public MovementType MovementType { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Unidades del movimiento. Para <see cref="MovementType.Adjustment"/> es el stock final deseado.</summary>
    public int Quantity { get; set; }

    /// <summary>Precio unitario del movimiento; para entradas es el costo de compra.</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>Stock del producto antes de aplicar este movimiento.</summary>
    public int StockBefore { get; set; }

    /// <summary>Stock del producto después de aplicar este movimiento. Permite auditar sin recalcular.</summary>
    public int StockAfter { get; set; }

    /// <summary>
    /// Variación neta que produjo este movimiento. Es lo que se invierte al revertirlo,
    /// y funciona igual para entradas, salidas y ajustes.
    /// </summary>
    public int Delta => StockAfter - StockBefore;

    public DateTime MovementDate { get; set; } = DateTime.UtcNow;

    /// <summary>Usuario que registró el movimiento.</summary>
    public Guid UserId { get; set; }

    public User? User { get; set; }

    /// <summary>Sólo aplica a salidas asociadas a una venta.</summary>
    public Guid? ClientId { get; set; }

    public Client? Client { get; set; }

    /// <summary>Sólo aplica a entradas: de qué proveedor llegó la mercadería.</summary>
    public Guid? SupplierId { get; set; }

    public Supplier? Supplier { get; set; }

    /// <summary>
    /// Cuando este movimiento es la reversión de otro, apunta al movimiento corregido.
    /// El original no se modifica nunca: la corrección se registra como un asiento nuevo.
    /// </summary>
    public Guid? ReversalOfMovementId { get; set; }

    public InventoryMovement? ReversalOfMovement { get; set; }

    public string? Comment { get; set; }

    public bool IsReversal => ReversalOfMovementId.HasValue;
}
