using StockHex_API.Domain.Common;

namespace StockHex_API.Application.DTOs;

/// <summary>Envoltorio de listados paginados que devuelve la API.</summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPrevious,
    bool HasNext)
{
    /// <summary>Proyecta una página del dominio al DTO de respuesta aplicando <paramref name="map"/> a cada elemento.</summary>
    public static PagedResponse<T> From<TSource>(PagedResult<TSource> source, Func<TSource, T> map) =>
        new(
            source.Items.Select(map).ToList(),
            source.Page,
            source.PageSize,
            source.TotalCount,
            source.TotalPages,
            source.HasPrevious,
            source.HasNext);
}
