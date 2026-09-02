using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class GetUserById
{
    private readonly IUserRepository _users;

    public GetUserById(IUserRepository users) => _users = users;

    public async Task<Result<UserResponse>> RunAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);

        return user is null
            ? Result<UserResponse>.Failure(Error.NotFound("Usuario", id))
            : user.ToResponse();
    }
}
