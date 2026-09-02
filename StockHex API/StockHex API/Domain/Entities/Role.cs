namespace StockHex_API.Domain.Entities;

/// <summary>
/// Un rol es un conjunto de permisos con nombre. A diferencia del catálogo de
/// permisos, los roles SON datos: se crean, editan y eliminan desde la interfaz.
/// </summary>
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Rol protegido. No se puede eliminar ni dejar sin los permisos críticos:
    /// sin él se perdería el acceso a la propia administración del sistema.
    /// </summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();

    public ICollection<User> Users { get; set; } = new List<User>();

    public IEnumerable<string> PermissionKeys => Permissions.Select(p => p.Permission);

    public bool Grants(string permission) =>
        Permissions.Any(p => p.Permission == permission);
}
