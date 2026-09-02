using Microsoft.EntityFrameworkCore;
using StockHex_API.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace StockHex_API.Tests.Database;

/// <summary>
/// Levanta un SQL Server real en un contenedor y le aplica las migraciones.
///
/// El resto de la suite corre sobre el proveedor InMemory, que verifica reglas de
/// negocio pero <b>no es relacional</b>: ignora los índices únicos, los índices
/// filtrados, las colaciones y el token de concurrencia. Justo las garantías que el
/// diseño delega en la base — que un movimiento no se revierta dos veces, que dos
/// SKU no colisionen, que dos movimientos simultáneos no se pisen el stock — no las
/// cubría ningún test. Estos sí.
///
/// La colección se comparte para pagar el arranque del contenedor una sola vez.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    /// <summary>
    /// La misma imagen del docker-compose, para probar contra el motor que se
    /// despliega y no contra un pariente cercano.
    /// </summary>
    private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        // xunit crea el fixture de la colección aunque todos sus tests estén
        // marcados como omitidos, así que sin esta guarda una máquina sin Docker
        // vería fallar la colección entera en lugar de saltársela.
        if (!DockerAvailability.IsAvailable)
            return;

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Se aplican las migraciones de verdad: si una queda a medias o el índice
        // filtrado no es válido en SQL Server, se ve aquí y no en producción.
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() =>
        DockerAvailability.IsAvailable ? _container.DisposeAsync().AsTask() : Task.CompletedTask;

    /// <summary>Un contexto nuevo por llamada: el rastreador no se comparte entre tests.</summary>
    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}

/// <summary>
/// Agrupa los tests que comparten el contenedor. Sin esto xunit correría las clases
/// en paralelo y cada una levantaría el suyo.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "sqlserver";
}
