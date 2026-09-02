using Microsoft.EntityFrameworkCore;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Tests.Common;

/// <summary>
/// Crea un <see cref="ApplicationDbContext"/> respaldado por el proveedor InMemory,
/// con un nombre de base distinto por test para que no compartan estado.
/// </summary>
internal static class TestDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"stockhex-tests-{Guid.NewGuid()}")
            // El proveedor InMemory no es relacional: avisa de que no aplica
            // las restricciones reales. Ese aviso se silencia porque estos tests
            // verifican reglas de negocio, no constraints de SQL Server.
            .ConfigureWarnings(w => w.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .EnableSensitiveDataLogging()
            .Options;

        return new ApplicationDbContext(options);
    }
}
