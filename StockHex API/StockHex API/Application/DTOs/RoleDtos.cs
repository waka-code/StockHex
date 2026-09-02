namespace StockHex_API.Application.DTOs;

public sealed record CreateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record UpdateRoleRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Permissions);

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    int PermissionCount,
    int UserCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Rol resumido, para incrustar en la respuesta de un usuario.</summary>
public sealed record RoleSummary(Guid Id, string Name, bool IsSystem);

/// <summary>
/// Una entrada del catálogo de permisos. El catálogo vive en el código y se expone
/// aquí para que el frontend no lo redeclare (regla 7).
/// </summary>
public sealed record PermissionResponse(
    string Key,
    string Module,
    string ModuleLabel,
    string Action,
    string ActionLabel,
    bool IsSpecial);

/// <summary>El catálogo completo, agrupado por módulo para dibujar la matriz.</summary>
public sealed record PermissionCatalogResponse(
    IReadOnlyList<PermissionResponse> Permissions,
    IReadOnlyList<PermissionModuleResponse> Modules,
    IReadOnlyList<PermissionActionResponse> StandardActions,
    int TotalCount);

public sealed record PermissionModuleResponse(
    string Module,
    string Label,
    IReadOnlyList<string> Permissions);

public sealed record PermissionActionResponse(string Action, string Label);
