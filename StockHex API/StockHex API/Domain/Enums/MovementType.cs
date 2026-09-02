namespace StockHex_API.Domain.Enums;

/// <summary>Tipo de movimiento de inventario y su efecto sobre el stock.</summary>
public enum MovementType
{
    /// <summary>Entrada de mercadería: suma al stock.</summary>
    In = 1,

    /// <summary>Salida / venta: resta del stock.</summary>
    Out = 2,

    /// <summary>Ajuste manual: fija el stock al valor indicado.</summary>
    Adjustment = 3
}
