using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;

namespace StockHex_API.Application.UseCases.RoleUseCases;

/// <summary>
/// Impide que administrar roles sea un atajo para volverse superusuario.
///
/// <c>roles.edit</c> sólo dice «puede configurar roles», pero sin más control
/// alcanza para todo: quien lo tiene edita su propio rol, marca las casillas que le
/// faltan y sale con permisos que nadie le concedió. El guardia lo corta con una
/// sola regla: <b>nadie concede un permiso que él mismo no tiene</b>.
///
/// Se comprueban únicamente los permisos que el cambio <b>añade</b>. Quitar, o
/// reenviar sin tocar los que el rol ya concedía, no es escalada, así que editar el
/// nombre de un rol más poderoso que el propio sigue funcionando.
///
/// El rol de sistema concede todo el catálogo (lo mantiene así
/// <c>PermissionSynchronizer</c>), de modo que quien administra de verdad no
/// encuentra ninguna puerta cerrada y no hace falta ninguna excepción para él.
/// </summary>
public sealed class PermissionEscalationGuard
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionResolver _permissions;

    public PermissionEscalationGuard(ICurrentUser currentUser, IPermissionResolver permissions)
    {
        _currentUser = currentUser;
        _permissions = permissions;
    }

    /// <param name="wanted">Permisos con los que quedaría el rol.</param>
    /// <param name="current">Los que ya concedía. Vacío al crear.</param>
    /// <returns>El error a devolver, o null si el cambio es admisible.</returns>
    public async Task<Error?> RejectEscalationAsync(
        IEnumerable<string> wanted,
        IEnumerable<string> current,
        CancellationToken cancellationToken = default)
    {
        var roleId = _currentUser.RoleId;
        if (roleId is null)
            return Error.Unauthorized("Se requiere un usuario autenticado para configurar roles.");

        var added = wanted
            .ToHashSet(StringComparer.Ordinal);
        added.ExceptWith(current);

        if (added.Count == 0)
            return null;

        var own = await _permissions.GetForRoleAsync(roleId.Value, cancellationToken);

        // Se recorre el catálogo y no el conjunto para que el mensaje salga en el
        // mismo orden que la matriz de la interfaz.
        var excess = Permissions.Catalog
            .Select(d => d.Key)
            .Where(key => added.Contains(key) && !own.Contains(key))
            .ToList();

        if (excess.Count == 0)
            return null;

        return Error.Forbidden(
            "No puedes conceder permisos que tu propio rol no tiene: " +
            $"{string.Join(", ", excess)}.");
    }
}
