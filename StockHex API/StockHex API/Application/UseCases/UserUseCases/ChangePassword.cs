using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class ChangePassword
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public ChangePassword(IUserRepository users, IPasswordHasher passwordHasher, IUnitOfWork unitOfWork)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NewPassword != request.ConfirmPassword)
            return Result.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.ConfirmPassword)] = ["Las contraseñas no coinciden."]
            }));

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("Usuario", userId));

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure(Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(request.CurrentPassword)] = ["La contraseña actual es incorrecta."]
            }));

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
