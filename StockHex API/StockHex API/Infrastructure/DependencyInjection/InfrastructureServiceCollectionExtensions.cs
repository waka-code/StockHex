using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using StockHex_API.Application.Abstractions;
using StockHex_API.Infrastructure.BackgroundServices;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;
using StockHex_API.Infrastructure.Repositories;
using StockHex_API.Infrastructure.Security;

namespace StockHex_API.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Valida y devuelve la cadena de conexión. Comprueba por cadena vacía y no
    /// sólo por null: appsettings.json deja la clave presente pero en blanco, así
    /// que un '?? throw' no detectaría el caso más probable en un despliegue nuevo
    /// y la aplicación arrancaría para fallar luego con un 500 opaco por petición.
    /// </summary>
    public static string RequireConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Falta ConnectionStrings:DefaultConnection. Configúralo por appsettings o por " +
                "la variable de entorno ConnectionStrings__DefaultConnection.");

        return connectionString;
    }

    /// <summary>Persistencia: DbContext, unidad de trabajo, repositorios y seeder.</summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            // La cadena se lee al resolver el contexto y no al registrarlo: durante
            // el registro, builder.Configuration todavía no incluye las fuentes que
            // añade un host externo (por ejemplo WebApplicationFactory en los tests),
            // así que leerla aquí es lo que hace que la configuración final mande.
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

            options.UseSqlServer(
                RequireConnectionString(configuration),
                sql => sql.EnableRetryOnFailure());
        });

        // El contexto es la implementación de IUnitOfWork; se resuelve la misma
        // instancia scoped que usan los repositorios para que compartan el tracker.
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInventoryMovementRepository, InventoryMovementRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    /// <summary>Tareas periódicas de mantenimiento.</summary>
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RefreshTokenCleanupOptions>()
            .Bind(configuration.GetSection(RefreshTokenCleanupOptions.SectionName));

        services.AddHostedService<RefreshTokenCleanupService>();

        return services;
    }

    /// <summary>Autenticación JWT, hashing de contraseñas y acceso al usuario en curso.</summary>
    public static IServiceCollection AddSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            // Falla al arrancar, no en el primer login.
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // La configuración del bearer se aplica por IConfigureNamedOptions para que
        // lea JwtOptions al resolverse, igual que TokenService.
        services.ConfigureOptions<JwtBearerOptionsSetup>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddAuthorization();

        return services;
    }
}
