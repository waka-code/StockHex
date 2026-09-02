using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class GetUsers
{
    private readonly IUserRepository _users;

    public GetUsers(IUserRepository users) => _users = users;

    public async Task<Result<PagedResponse<UserResponse>>> RunAsync(
        UserFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _users.GetPagedAsync(filter, cancellationToken);
        return PagedResponse<UserResponse>.From(page, u => u.ToResponse());
    }
}
