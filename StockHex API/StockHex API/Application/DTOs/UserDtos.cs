using StockHex_API.Domain.Enums;

namespace StockHex_API.Application.DTOs;

public sealed record CreateUserRequest(
    string Name,
    string Email,
    string Password,
    string ConfirmPassword,
    UserRole Role);

/// <summary>Actualiza el perfil; la contraseña se cambia por su propio endpoint.</summary>
public sealed record UpdateUserRequest(
    string Name,
    string Email,
    UserRole Role,
    bool IsActive);

public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword);

/// <summary>Nunca expone el hash de la contraseña.</summary>
public sealed record UserResponse(
    Guid Id,
    string Name,
    string Email,
    UserRole Role,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt);
