using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class CreateUser
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUser(IUserRepository users, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> RunAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (request.Password != request.ConfirmPassword)
            return Result<UserResponse>.Failure(
                Error.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.ConfirmPassword)] = ["Las contraseñas no coinciden."]
                }));

        // Comprobación contra la base de datos, no cargando la tabla completa en memoria.
        if (await _users.ExistsByEmailAsync(email, null, cancellationToken))
            return Result<UserResponse>.Failure(
                Error.Conflict($"Ya existe un usuario con el email '{email}'."));

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role
        };

        await _users.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.ToResponse();
    }
}
