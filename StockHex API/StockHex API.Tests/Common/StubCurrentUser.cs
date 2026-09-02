using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Tests.Common;

/// <summary>Sustituye la identidad del JWT en los tests de use cases.</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public StubCurrentUser(Guid? id = null, UserRole? role = UserRole.Operator, string? email = null)
    {
        Id = id;
        Role = role;
        Email = email;
    }

    public Guid? Id { get; }

    public string? Email { get; }

    public UserRole? Role { get; }

    public bool IsAuthenticated => Id.HasValue;
}
