using StockHex_API.Application.DTOs;
using StockHex_API.Application.Mappings;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class UpdateUser
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateUser(IUserRepository users, IUnitOfWork unitOfWork)
    {
        _users = users;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<UserResponse>> RunAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return Result<UserResponse>.Failure(Error.NotFound("Usuario", id));

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(email, id, cancellationToken))
            return Result<UserResponse>.Failure(
                Error.Conflict($"Ya existe otro usuario con el email '{email}'."));

        // Sin este guardia se puede dejar el sistema sin ningún administrador activo.
        var losesAdmin = user.Role == UserRole.Admin &&
                         (request.Role != UserRole.Admin || !request.IsActive);

        if (losesAdmin && await _users.CountByRoleAsync(UserRole.Admin, cancellationToken) <= 1)
            return Result<UserResponse>.Failure(Error.Conflict(
                "No se puede degradar ni desactivar al único administrador del sistema."));

        user.Name = request.Name.Trim();
        user.Email = email;
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        _users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user.ToResponse();
    }
}
