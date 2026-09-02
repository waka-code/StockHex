using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Infrastructure.Persistence;

/// <summary>
/// Reconcilia lo que hay en <c>RolePermissions</c> con el catálogo del código.
///
/// El catálogo vive en <see cref="Permissions"/> y añadir una clave es un cambio de
/// código (regla 7). Los permisos de los roles, en cambio, son filas: los sembró una
/// migración y desde entonces los edita quien administra. Esas dos cosas se separan
/// solas, y en las dos direcciones:
///
/// <list type="bullet">
/// <item>Un rol <c>IsSystem</c> se describe como «acceso total», pero su lista es la
/// foto del catálogo del día en que se escribió la migración. Al agregar un permiso
/// nuevo, el rol de sistema NO lo tiene: el endpoint responde 403 hasta al
/// administrador, en silencio, hasta que alguien marque la casilla a mano.</item>
/// <item>Al quitar una clave del catálogo, las filas que la conceden quedan
/// referenciando algo que ya no comprueba nadie. No dan acceso a nada, pero
/// aparecen en la matriz y hacen creer que sí.</item>
/// </list>
///
/// Por eso el rol de sistema se <b>deriva</b> del catálogo en vez de guardarse: es
/// «todos los permisos que existan hoy», no «los que existían al migrar».
/// </summary>
public sealed class PermissionSynchronizer
{
    private readonly ApplicationDbContext _context;
    private readonly IPermissionResolver _permissions;
    private readonly ILogger<PermissionSynchronizer> _logger;

    public PermissionSynchronizer(
        ApplicationDbContext context,
        IPermissionResolver permissions,
        ILogger<PermissionSynchronizer> logger)
    {
        _context = context;
        _permissions = permissions;
        _logger = logger;
    }

    public async Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.Roles
            .Include(r => r.Permissions)
            .ToListAsync(cancellationToken);

        var granted = 0;
        var removed = 0;
        var touched = new List<Role>();

        foreach (var role in roles)
        {
            var before = role.Permissions.Count;

            // Claves que ya no existen en el código: no las comprueba ningún
            // [RequirePermission], así que no conceden nada. Se quitan de todos los
            // roles, no sólo del de sistema.
            var obsolete = role.Permissions
                .Where(p => !Permissions.Exists(p.Permission))
                .ToList();

            foreach (var permission in obsolete)
            {
                role.Permissions.Remove(permission);
                _context.RolePermissions.Remove(permission);
            }

            removed += obsolete.Count;

            if (role.IsSystem)
            {
                var held = role.Permissions
                    .Select(p => p.Permission)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var key in Permissions.All.Where(k => !held.Contains(k)))
                {
                    var permission = new RolePermission { RoleId = role.Id, Permission = key };

                    role.Permissions.Add(permission);

                    // Add explícito, igual que en RoleRepository.ReplacePermissions:
                    // la entidad ya trae un Id del inicializador de la propiedad, y
                    // al aparecer en un grafo rastreado EF deduce del PK no vacío que
                    // la fila ya existe y la marca Modified en vez de Added.
                    _context.RolePermissions.Add(permission);
                    granted++;
                }
            }

            if (role.Permissions.Count != before)
            {
                role.UpdatedAt = DateTime.UtcNow;
                touched.Add(role);
            }
        }

        if (touched.Count == 0)
        {
            _logger.LogDebug("Los permisos de los roles ya coinciden con el catálogo.");
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // La caché del resolver es por proceso y de 30 s. Al arrancar está vacía,
        // pero invalidar es lo correcto y cuesta nada: deja el método utilizable
        // también fuera del arranque.
        foreach (var role in touched)
            _permissions.Invalidate(role.Id);

        _logger.LogInformation(
            "Permisos sincronizados con el catálogo: {Granted} concedido(s) a roles de sistema " +
            "y {Removed} clave(s) obsoleta(s) eliminada(s), en {Roles} rol(es).",
            granted, removed, touched.Count);
    }
}
