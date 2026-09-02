namespace StockHex_API.Domain.Entities;

/// <summary>
/// Un permiso concedido a un rol. Guarda la CLAVE del catálogo, no una FK: el
/// catálogo vive en el código y no tiene tabla (regla 7). La validez de la clave
/// se comprueba contra <see cref="Authorization.Permissions.All"/> al escribir.
/// </summary>
public class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RoleId { get; set; }

    public Role? Role { get; set; }

    /// <summary>Clave del catálogo, por ejemplo <c>products.create</c>.</summary>
    public string Permission { get; set; } = string.Empty;
}
