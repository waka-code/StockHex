namespace StockHex_API.Domain.Enums;

/// <summary>Roles del sistema, usados para autorización basada en claims.</summary>
public enum UserRole
{
    /// <summary>Acceso total, incluida la gestión de usuarios.</summary>
    Admin = 1,

    /// <summary>Gestiona catálogo, clientes, proveedores y movimientos.</summary>
    Manager = 2,

    /// <summary>Sólo registra movimientos de inventario y consulta.</summary>
    Operator = 3
}
