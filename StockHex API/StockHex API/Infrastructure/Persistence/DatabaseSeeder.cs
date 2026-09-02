using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Infrastructure.Persistence;

/// <summary>
/// Crea el administrador inicial si la base no tiene ninguno. Sin esto no habría
/// forma de usar los endpoints protegidos en una instalación nueva.
///
/// Los ROLES los crea la migración, no este seeder: forman parte del esquema
/// mínimo y tienen que existir antes de poder insertar cualquier usuario.
/// </summary>
public sealed class DatabaseSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IConfiguration configuration,
        ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // «Administrador» ya no es un enum: es quien tiene los permisos críticos.
        var hasAdmin = await _context.Users.AnyAsync(
            u => u.IsActive && u.Role!.Permissions.Any(p => p.Permission == Permissions.Roles.Edit),
            cancellationToken);

        if (hasAdmin)
            return;

        var email = _configuration["Seed:AdminEmail"];
        var password = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "No hay ningún usuario activo con permiso para administrar roles y no se " +
                "configuró Seed:AdminEmail / Seed:AdminPassword. Define esas claves (o las " +
                "variables Seed__AdminEmail y Seed__AdminPassword) para crear el primer administrador.");
            return;
        }

        var systemRole = await _context.Roles
            .FirstOrDefaultAsync(r => r.IsSystem, cancellationToken);

        if (systemRole is null)
        {
            _logger.LogError(
                "No existe el rol de sistema. La migración debería haberlo creado; " +
                "no se puede sembrar el administrador inicial.");
            return;
        }

        var admin = new User
        {
            Name = _configuration["Seed:AdminName"] ?? "Administrador",
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(password),
            RoleId = systemRole.Id,
            IsActive = true,
            EmailConfirmed = true,
        };

        _context.Users.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Administrador inicial creado con el email {Email} y el rol {Role}.",
            admin.Email, systemRole.Name);
    }
}
