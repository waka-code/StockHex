namespace StockHex_API.Application.DTOs;

public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string ConfirmPassword,
    Guid RoleId);

/// <summary>Actualiza el perfil; la contraseña se cambia por su propio endpoint.</summary>
public sealed record UpdateUserRequest(
    string Name,
    string Email,
    Guid RoleId,
    bool IsActive);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

/// <summary>
/// Restablecimiento hecho por otra persona: no pide la contraseña actual, porque
/// quien la cambia no la conoce. Exige el permiso users.change_password.
/// </summary>
public sealed record ResetPasswordRequest(
    string NewPassword,
    string ConfirmPassword,
    /// <summary>Revoca los tokens de refresco del usuario, forzándolo a entrar de nuevo.</summary>
    bool RevokeSessions = true);

/// <summary>Nunca expone el hash de la contraseña.</summary>
public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    RoleSummary Role,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt);

/// <summary>
/// Perfil del usuario autenticado. Incluye los permisos efectivos para que el
/// frontend no ofrezca acciones que van a fallar; la autorización real la impone
/// la API en cada endpoint.
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Name,
    string Email,
    RoleSummary Role,
    IReadOnlyList<string> Permissions,
    bool IsActive,
    DateTime? LastLoginAt);
