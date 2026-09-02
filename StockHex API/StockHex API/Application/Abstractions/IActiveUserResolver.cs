namespace StockHex_API.Application.Abstractions;

/// <summary>
/// Dice si la cuenta detrás de un token sigue existiendo y activa.
///
/// El JWT no se puede revocar: una vez emitido vale hasta que expira. Sin esta
/// comprobación, desactivar o borrar a alguien no lo echa — el refresco le falla,
/// pero su access token en curso sigue abriendo todos los endpoints hasta una hora
/// después (ocho en <c>Development</c>, donde dura 480 minutos).
///
/// Se resuelve por petición con la misma caché corta que
/// <see cref="IPermissionResolver"/>: es el mismo compromiso entre que el cambio
/// surta efecto pronto y no ir a la base en cada llamada.
/// </summary>
public interface IActiveUserResolver
{
    Task<bool> IsActiveAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalida la caché del usuario. Se llama al desactivarlo o eliminarlo para
    /// que quede fuera de inmediato y no al vencer la entrada.
    /// </summary>
    void Invalidate(Guid userId);
}
