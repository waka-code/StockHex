using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>Devuelve el perfil del portador del token, para que el cliente no lo tenga que cachear.</summary>
public sealed class GetCurrentUser
{
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;

    public GetCurrentUser(IUserRepository users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<Result<UserResponse>> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_currentUser.Id is null)
            return Result<UserResponse>.Failure(Error.Unauthorized("No hay un usuario autenticado."));

        var user = await _users.GetByIdAsync(_currentUser.Id.Value, cancellationToken);

        return user is null
            ? Result<UserResponse>.Failure(Error.Unauthorized("El usuario del token ya no existe."))
            : user.ToResponse();
    }
}
