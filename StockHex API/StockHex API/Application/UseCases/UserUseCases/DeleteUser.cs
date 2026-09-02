using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Enums;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.UserUseCases;

public sealed class DeleteUser
{
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUser(IUserRepository users, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _users = users;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> RunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound("Usuario", id));

        if (_currentUser.Id == id)
            return Result.Failure(Error.Conflict("Un usuario no puede eliminar su propia cuenta."));

        if (user.Role == UserRole.Admin &&
            await _users.CountByRoleAsync(UserRole.Admin, cancellationToken) <= 1)
            return Result.Failure(Error.Conflict(
                "No se puede eliminar al único administrador del sistema."));

        // Los movimientos referencian al usuario con FK restrictiva, así que se desactiva
        // en lugar de borrar cuando ya registró actividad.
        var movementCount = await _users.CountMovementsAsync(id, cancellationToken);

        if (movementCount > 0 || !user.IsActive)
        {
            if (!user.IsActive)
                return Result.Failure(Error.Conflict("El usuario ya está desactivado."));

            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            _users.Update(user);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure(Error.Conflict(
                $"El usuario registró {movementCount} movimiento(s) de inventario, por lo que " +
                "se desactivó en lugar de eliminarse para conservar la auditoría."));
        }

        _users.Remove(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
