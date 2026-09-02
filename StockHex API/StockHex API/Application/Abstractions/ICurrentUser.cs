using StockHex_API.Domain.Enums;

namespace StockHex_API.Application.Abstractions;

/// <summary>Identidad del usuario autenticado, leída del JWT de la petición en curso.</summary>
public interface ICurrentUser
{
    Guid? Id { get; }

    string? Email { get; }

    UserRole? Role { get; }

    bool IsAuthenticated { get; }
}
