using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Common;

namespace StockHex_API.Application.UseCases.RoleUseCases;

/// <summary>
/// Expone el catálogo de permisos que vive en el código, agrupado por módulo para
/// que el frontend dibuje la matriz sin volver a declararlo (regla 7).
/// </summary>
public sealed class GetPermissionCatalog
{
    /// <summary>Las cuatro acciones que forman la rejilla; el resto va en «especiales».</summary>
    private static readonly PermissionActionResponse[] StandardActions =
    [
        new("view", "Ver"),
        new("create", "Crear"),
        new("edit", "Editar"),
        new("delete", "Eliminar"),
    ];

    public Result<PermissionCatalogResponse> Run()
    {
        var permissions = Permissions.Catalog.Select(d => d.ToResponse()).ToList();

        var modules = Permissions.Catalog
            .GroupBy(d => (d.Module, d.ModuleLabel))
            .Select(g => new PermissionModuleResponse(
                g.Key.Module,
                g.Key.ModuleLabel,
                g.Select(d => d.Key).ToList()))
            .ToList();

        return new PermissionCatalogResponse(
            permissions,
            modules,
            StandardActions,
            permissions.Count);
    }
}
