namespace StockHex_API.Domain.Authorization;

/// <summary>
/// Fuente única del catálogo de permisos (regla 7 de CLAUDE.md).
///
/// No hay tabla de permisos ni siembra: un permiso existe porque un endpoint lo
/// comprueba con <c>[RequirePermission]</c>, así que el código es la autoridad.
/// La API expone este catálogo en <c>GET /api/permissions</c> y el frontend lo
/// consume sin volver a declararlo.
///
/// Los roles guardan las CLAVES que conceden, validadas contra <see cref="All"/>
/// al escribir. Añadir un permiso es un cambio de código, igual que el endpoint
/// que lo comprueba, porque son la misma cosa.
/// </summary>
public static class Permissions
{
    public static class Dashboard
    {
        public const string View = "dashboard.view";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Create = "products.create";
        public const string Edit = "products.edit";
        public const string Delete = "products.delete";
    }

    public static class Movements
    {
        public const string View = "movements.view";
        public const string Create = "movements.create";
        public const string Reverse = "movements.reverse";
    }

    public static class Categories
    {
        public const string View = "categories.view";
        public const string Create = "categories.create";
        public const string Edit = "categories.edit";
        public const string Delete = "categories.delete";
    }

    public static class Suppliers
    {
        public const string View = "suppliers.view";
        public const string Create = "suppliers.create";
        public const string Edit = "suppliers.edit";
        public const string Delete = "suppliers.delete";
    }

    public static class Clients
    {
        public const string View = "clients.view";
        public const string Create = "clients.create";
        public const string Edit = "clients.edit";
        public const string Delete = "clients.delete";
    }

    public static class Reports
    {
        public const string View = "reports.view";
        public const string Export = "reports.export";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Edit = "users.edit";
        public const string Delete = "users.delete";
        public const string ChangePassword = "users.change_password";
    }

    public static class Roles
    {
        public const string View = "roles.view";
        public const string Create = "roles.create";
        public const string Edit = "roles.edit";
        public const string Delete = "roles.delete";
    }

    /// <summary>Metadatos de un permiso, para que la interfaz pueda dibujar la matriz.</summary>
    public sealed record Descriptor(
        string Key,
        string Module,
        string ModuleLabel,
        string Action,
        string ActionLabel,
        /// <summary>
        /// True cuando la acción no forma parte de las cuatro estándar
        /// (ver, crear, editar, eliminar) y la matriz la muestra aparte.
        /// </summary>
        bool IsSpecial);

    private const string View = "view";
    private const string Create = "create";
    private const string Edit = "edit";
    private const string Delete = "delete";

    /// <summary>Orden de los módulos en la matriz: el mismo que el del menú.</summary>
    public static readonly IReadOnlyList<Descriptor> Catalog =
    [
        new(Dashboard.View, "dashboard", "Dashboard", View, "Ver", false),

        new(Products.View, "products", "Productos", View, "Ver", false),
        new(Products.Create, "products", "Productos", Create, "Crear", false),
        new(Products.Edit, "products", "Productos", Edit, "Editar", false),
        new(Products.Delete, "products", "Productos", Delete, "Eliminar", false),

        new(Movements.View, "movements", "Movimientos", View, "Ver", false),
        new(Movements.Create, "movements", "Movimientos", Create, "Crear", false),
        new(Movements.Reverse, "movements", "Movimientos", "reverse", "Revertir", true),

        new(Categories.View, "categories", "Categorías", View, "Ver", false),
        new(Categories.Create, "categories", "Categorías", Create, "Crear", false),
        new(Categories.Edit, "categories", "Categorías", Edit, "Editar", false),
        new(Categories.Delete, "categories", "Categorías", Delete, "Eliminar", false),

        new(Suppliers.View, "suppliers", "Proveedores", View, "Ver", false),
        new(Suppliers.Create, "suppliers", "Proveedores", Create, "Crear", false),
        new(Suppliers.Edit, "suppliers", "Proveedores", Edit, "Editar", false),
        new(Suppliers.Delete, "suppliers", "Proveedores", Delete, "Eliminar", false),

        new(Clients.View, "clients", "Clientes", View, "Ver", false),
        new(Clients.Create, "clients", "Clientes", Create, "Crear", false),
        new(Clients.Edit, "clients", "Clientes", Edit, "Editar", false),
        new(Clients.Delete, "clients", "Clientes", Delete, "Eliminar", false),

        new(Reports.View, "reports", "Reportes", View, "Ver", false),
        new(Reports.Export, "reports", "Reportes", "export", "Exportar", true),

        new(Users.View, "users", "Usuarios", View, "Ver", false),
        new(Users.Create, "users", "Usuarios", Create, "Crear", false),
        new(Users.Edit, "users", "Usuarios", Edit, "Editar", false),
        new(Users.Delete, "users", "Usuarios", Delete, "Eliminar", false),
        new(Users.ChangePassword, "users", "Usuarios", "change_password", "Cambiar contraseña", true),

        new(Roles.View, "roles", "Roles", View, "Ver", false),
        new(Roles.Create, "roles", "Roles", Create, "Crear", false),
        new(Roles.Edit, "roles", "Roles", Edit, "Editar", false),
        new(Roles.Delete, "roles", "Roles", Delete, "Eliminar", false),
    ];

    /// <summary>Todas las claves del catálogo.</summary>
    public static readonly IReadOnlySet<string> All =
        Catalog.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Permisos sin los que nadie podría volver a administrar el sistema. Un rol
    /// activo tiene que conservarlos entre todos sus usuarios.
    /// </summary>
    public static readonly IReadOnlyList<string> Critical = [Roles.Edit, Users.Edit];

    public static bool Exists(string key) => All.Contains(key);

    /// <summary>Devuelve sólo las claves válidas, sin duplicados y en el orden del catálogo.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> keys)
    {
        var requested = keys
            .Select(k => k?.Trim() ?? string.Empty)
            .Where(All.Contains)
            .ToHashSet(StringComparer.Ordinal);

        return Catalog.Where(d => requested.Contains(d.Key)).Select(d => d.Key).ToList();
    }

    /// <summary>Claves solicitadas que no existen en el catálogo, para poder rechazarlas.</summary>
    public static IReadOnlyList<string> Unknown(IEnumerable<string> keys) =>
        keys.Select(k => k?.Trim() ?? string.Empty)
            .Where(k => k.Length > 0 && !All.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
