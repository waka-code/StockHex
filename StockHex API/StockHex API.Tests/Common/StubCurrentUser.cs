using StockHex_API.Application.Abstractions;

namespace StockHex_API.Tests.Common;

/// <summary>Sustituye la identidad del JWT en los tests de casos de uso.</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public StubCurrentUser(Guid? id = null, Guid? roleId = null, string? email = null, string? roleName = null)
    {
        Id = id;
        RoleId = roleId;
        Email = email;
        RoleName = roleName;
    }

    public Guid? Id { get; }

    public string? Email { get; }

    public Guid? RoleId { get; }

    public string? RoleName { get; }

    public bool IsAuthenticated => Id.HasValue;
}
