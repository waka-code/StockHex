using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>
/// Auto-registro público. Siempre crea el usuario con rol <see cref="UserRole.Operator"/>:
/// el rol nunca se toma del body para que nadie pueda registrarse como administrador.
/// </summary>
public sealed class Register
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IssueTokens _issueTokens;
    private readonly IUnitOfWork _unitOfWork;

    public Register(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IssueTokens issueTokens,
        IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _issueTokens = issueTokens;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<AuthResponse>> RunAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
            return Result<AuthResponse>.Failure(
                Error.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.ConfirmPassword)] = ["Las contraseñas no coinciden."]
                }));

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(email, null, cancellationToken))
            return Result<AuthResponse>.Failure(
                Error.Conflict($"Ya existe un usuario con el email '{email}'."));

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Operator,
            LastLoginAt = DateTime.UtcNow
        };

        await _users.AddAsync(user, cancellationToken);

        var (response, _) = await _issueTokens.RunAsync(user, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
