namespace StockHex_API.Api.Extensions;

/// <summary>
/// Nombres de rol usados en <c>[Authorize(Roles = ...)]</c>. Son constantes para
/// que un typo rompa la compilación en vez de dejar un endpoint sin proteger.
/// </summary>
public static class Roles
{
    public const string Admin = nameof(Domain.Enums.UserRole.Admin);
    public const string Manager = nameof(Domain.Enums.UserRole.Manager);

    /// <summary>Roles que pueden administrar el catálogo (categorías, productos, proveedores).</summary>
    public const string AdminOrManager = $"{Admin},{Manager}";
}
