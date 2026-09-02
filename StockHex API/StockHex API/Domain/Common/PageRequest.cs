using StockHex_API.Domain.Enums;

namespace StockHex_API.Domain.Common;

/// <summary>Parámetros de paginación y búsqueda comunes a todos los listados.</summary>
public class PageRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    private int _page = 1;
    private int _pageSize = DefaultPageSize;

    /// <summary>Página solicitada, 1-based. Valores menores a 1 se normalizan a 1.</summary>
    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    /// <summary>Tamaño de página, acotado a <see cref="MaxPageSize"/> para no permitir consultas ilimitadas.</summary>
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => DefaultPageSize,
            > MaxPageSize => MaxPageSize,
            _ => value
        };
    }

    /// <summary>Texto libre; cada repositorio decide sobre qué columnas busca.</summary>
    public string? Search { get; set; }

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>Filtros del listado de productos.</summary>
public sealed class ProductFilter : PageRequest
{
    public Guid? CategoryId { get; set; }

    public Guid? SupplierId { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>Cuando es true, sólo devuelve productos en o por debajo de su stock mínimo.</summary>
    public bool LowStockOnly { get; set; }
}

/// <summary>Filtros del listado de movimientos de inventario.</summary>
public sealed class MovementFilter : PageRequest
{
    public Guid? ProductId { get; set; }

    public Guid? ClientId { get; set; }

    public Guid? SupplierId { get; set; }

    public Guid? UserId { get; set; }

    public MovementType? MovementType { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }
}
