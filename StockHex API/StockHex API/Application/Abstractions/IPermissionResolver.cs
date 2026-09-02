namespace StockHex_API.Application.Abstractions;

/// <summary>
/// Resuelve los permisos efectivos de un rol.
///
/// El JWT lleva sólo el id del rol, no la lista de permisos: si los llevara,
/// quitarle un permiso a alguien no surtiría efecto hasta que su token se
/// renovara, hasta 60 minutos después. Resolviendo por petición con una caché
/// corta el cambio se aplica en menos de un minuto sin cerrar la sesión de nadie.
/// </summary>
public interface IPermissionResolver
{
    Task<IReadOnlySet<string>> GetForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task<bool> HasPermissionAsync(Guid roleId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida la caché del rol. Se llama al cambiar sus permisos para que el
    /// efecto sea inmediato y no haya que esperar a que caduque la entrada.
    /// </summary>
    void Invalidate(Guid roleId);
}
