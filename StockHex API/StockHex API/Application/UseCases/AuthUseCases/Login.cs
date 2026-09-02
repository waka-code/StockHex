using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

public sealed class Login
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IssueTokens _issueTokens;
    private readonly IUnitOfWork _unitOfWork;

    public Login(
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
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email, cancellationToken);

        // Mismo mensaje para email inexistente y contraseña incorrecta: no se filtra
        // qué emails están registrados.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Failure(
                Error.Unauthorized("Email o contraseña incorrectos."));

        if (!user.IsActive)
            return Result<AuthResponse>.Failure(
                Error.Unauthorized("La cuenta está desactivada."));

        var (response, _) = await _issueTokens.RunAsync(user, cancellationToken);

        user.LastLoginAt = DateTime.UtcNow;
        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
