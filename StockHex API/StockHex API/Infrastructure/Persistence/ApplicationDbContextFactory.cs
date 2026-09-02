using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StockHex_API.Infrastructure.Persistence;

/// <summary>
/// Usado sólo por las herramientas de EF (<c>dotnet ef migrations</c>), que necesitan
/// construir el contexto sin arrancar la aplicación.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            // Placeholder: sólo hace falta un connection string sintácticamente válido
            // para generar el scaffolding de una migración, no una base accesible.
            ?? "Server=localhost;Database=StockHex;Trusted_Connection=False;TrustServerCertificate=True;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
