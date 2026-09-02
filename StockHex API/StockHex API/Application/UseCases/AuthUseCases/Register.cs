using StockHex_API.Application.Abstractions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Common;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Application.UseCases.AuthUseCases;

/// <summary>
/// Auto-registro público. El rol nunca se toma del body: se usa el rol configurado
/// como predeterminado para registros, de modo que nadie pueda registrarse con
/// permisos elevados.
/// </summary>
public sealed class Register
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IssueTokens _issueTokens;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDefaultRoleProvider _defaultRole;

    public Register(
        IUserRepository users,
        IRoleRepository roles,
        IPasswordHasher passwordHasher,
        IssueTokens issueTokens,
        IUnitOfWork unitOfWork,
        IDefaultRoleProvider defaultRole)
    {
        _users = users;
        _roles = roles;
        _passwordHasher = passwordHasher;
        _issueTokens = issueTokens;
        _unitOfWork = unitOfWork;
        _defaultRole = defaultRole;
    }

    public async Task<Result<AuthResponse>> RunAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
            return Result<AuthResponse>.Failure(
                Error.Validation(new Dictionary<string, string[]>
                {
                    [nameof(request.ConfirmPassword)] = ["Las contraseñas no coinciden."],
                }));

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _users.ExistsByEmailAsync(email, null, cancellationToken))
            return Result<AuthResponse>.Failure(
                Error.Conflict($"Ya existe un usuario con el email '{email}'."));

        var roleId = await _defaultRole.GetRegistrationRoleIdAsync(cancellationToken);
        if (roleId is null)
            return Result<AuthResponse>.Failure(Error.Conflict(
                "No hay un rol configurado para los registros públicos. " +
                "Un administrador debe designarlo antes de habilitar el auto-registro."));

        var role = await _roles.GetByIdAsync(roleId.Value, includePermissions: false, cancellationToken);

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleId = roleId.Value,
            Role = role,
            LastLoginAt = DateTime.UtcNow,
        };

        await _users.AddAsync(user, cancellationToken);

        var (response, _) = await _issueTokens.RunAsync(user, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return response;
    }
}
