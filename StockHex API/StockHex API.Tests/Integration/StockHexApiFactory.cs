using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockHex_API.Application.Abstractions;
using StockHex_API.Domain.Authorization;
using StockHex_API.Domain.Entities;
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

    /// <summary>
    /// Siembra los roles y los usuarios de prueba. La migración que los crea no
    /// corre sobre InMemory, así que aquí se replica lo mínimo: un rol con el
    /// catálogo completo y otro de menor privilegio.
    /// </summary>
    public async Task SeedUsersAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        if (await context.Users.AnyAsync())
            return;

        var adminRole = new Role
        {
            Name = "Administrador",
            Description = "Acceso total al sistema",
            IsSystem = true,
        };
        foreach (var key in Permissions.All)
            adminRole.Permissions.Add(new RolePermission { RoleId = adminRole.Id, Permission = key });

        var operatorRole = new Role
        {
            Name = "Bodeguero",
            Description = "Registra movimientos y consulta el catálogo",
            IsSystem = false,
        };
        foreach (var key in new[]
                 {
                     Permissions.Dashboard.View,
                     Permissions.Products.View,
                     Permissions.Movements.View,
                     Permissions.Movements.Create,
                     Permissions.Reports.View,
                 })
        {
            operatorRole.Permissions.Add(new RolePermission { RoleId = operatorRole.Id, Permission = key });
        }

        context.Roles.AddRange(adminRole, operatorRole);

        context.Users.AddRange(
            new User
            {
                Name = "Admin",
                Email = AdminEmail,
                PasswordHash = hasher.Hash(AdminPassword),
                RoleId = adminRole.Id,
            },
            new User
            {
                Name = "Operador",
                Email = OperatorEmail,
                PasswordHash = hasher.Hash(OperatorPassword),
                RoleId = operatorRole.Id,
            });

        await context.SaveChangesAsync();
    }

    /// <summary>Id del rol de menor privilegio, para las pruebas que asignan roles.</summary>
    public async Task<Guid> GetOperatorRoleIdAsync()
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Roles.Where(r => !r.IsSystem).Select(r => r.Id).FirstAsync();
    }

    public async Task<ApplicationDbContext> CreateContextAsync()
    {
        var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
