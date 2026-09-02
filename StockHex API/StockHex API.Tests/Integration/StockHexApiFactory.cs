using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Enums;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Tests.Integration;

/// <summary>
/// Levanta la API real en memoria, sustituyendo SQL Server por el proveedor InMemory.
/// Ejercita el pipeline completo: autenticación, autorización, FluentValidation,
/// el middleware de errores y el mapeo de Result a HTTP.
/// </summary>
public class StockHexApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"stockhex-integration-{Guid.NewGuid()}";

    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Password123";
    public const string OperatorEmail = "operator@test.local";
    public const string OperatorPassword = "Password123";

    /// <summary>Permite a las subclases sobreescribir claves de configuración.</summary>
    protected virtual IReadOnlyDictionary<string, string?> ConfigurationOverrides =>
        new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // El proveedor se reemplaza por InMemory más abajo, pero la
                // comprobación de arranque exige una cadena no vacía.
                ["ConnectionStrings:DefaultConnection"] = "Server=(test);Database=StockHex;",
                ["Jwt:Issuer"] = "StockHexTests",
                ["Jwt:Audience"] = "StockHexClient",
                ["Jwt:Key"] = "clave-de-pruebas-suficientemente-larga-para-hmac256",
                ["Jwt:AccessTokenMinutes"] = "30",
                // El seeder y las migraciones no aplican sobre InMemory.
                ["Database:MigrateOnStartup"] = "false",
                // Todas las peticiones de TestServer comparten partición (no hay IP
                // remota), así que el límite real ahogaría la suite. El limitador se
                // verifica aparte, en RateLimitedApiFactory con un límite bajo.
                ["RateLimiting:AuthPermitLimit"] = "10000"
            });
        });

        // Se añade después del bloque anterior, así que gana sobre él.
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            if (ConfigurationOverrides.Count > 0)
                configuration.AddInMemoryCollection(ConfigurationOverrides);
        });

        builder.ConfigureServices(services =>
        {
            // Quita el DbContext de SQL Server registrado por AddPersistence.
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>Crea un administrador y un operador para poder autenticarse en los tests.</summary>
    public async Task SeedUsersAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await context.Users.AnyAsync())
            return;

        context.Users.AddRange(
            new User
            {
                Name = "Admin",
                Email = AdminEmail,
                PasswordHash = hasher.Hash(AdminPassword),
                Role = UserRole.Admin
            },
            new User
            {
                Name = "Operador",
                Email = OperatorEmail,
                PasswordHash = hasher.Hash(OperatorPassword),
                Role = UserRole.Operator
            });

        await context.SaveChangesAsync();
    }

    public async Task<ApplicationDbContext> CreateContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
