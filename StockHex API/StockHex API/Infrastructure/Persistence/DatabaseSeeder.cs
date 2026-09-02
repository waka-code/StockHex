using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Infrastructure.Persistence;

/// <summary>
/// Crea el administrador inicial si la base no tiene ninguno. Sin esto no habría
/// forma de usar los endpoints protegidos en una instalación nueva.
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
        if (await _context.Users.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
            return;

        var email = _configuration["Seed:AdminEmail"];
        var password = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "No hay administrador y no se configuró Seed:AdminEmail / Seed:AdminPassword. " +
                "Define esas claves (o las variables Seed__AdminEmail y Seed__AdminPassword) " +
                "para crear el primer administrador.");
            return;
        }

        var admin = new User
        {
            Name = _configuration["Seed:AdminName"] ?? "Administrador",
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(password),
            Role = UserRole.Admin,
            IsActive = true,
            EmailConfirmed = true
        };

        _context.Users.Add(admin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Administrador inicial creado con el email {Email}.", admin.Email);
    }
}
