namespace StockHex_API.Application.Abstractions;

/// <summary>Identidad del usuario autenticado, leída del JWT de la petición en curso.</summary>
public interface ICurrentUser
{
    Guid? Id { get; }

    string? Email { get; }

    /// <summary>
    /// Id del rol. El token no lleva la lista de permisos: se resuelve con
    /// <see cref="IPermissionResolver"/> para que un cambio surta efecto sin
    /// esperar a que el token se renueve.
    /// </summary>
    Guid? RoleId { get; }

    /// <summary>Nombre del rol, sólo para mostrar. Nunca para decidir autorización.</summary>
    string? RoleName { get; }

    bool IsAuthenticated { get; }
}
