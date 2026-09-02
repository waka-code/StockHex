using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;

namespace StockHex_API.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IUnitOfWork
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Intentos de una operación que choca por concurrencia. Los movimientos de un
    /// mismo producto compiten por <see cref="Product.RowVersion"/>; sin reintento,
    /// un producto con rotación alta rechazaba la mayoría de peticiones simultáneas.
    /// </summary>
    /// <remarks>
    /// Eran 5 con espera lineal. Medido contra SQL Server real, 25 movimientos
    /// simultáneos sobre un mismo producto agotaban los intentos: con esa cantidad
    /// de escritores compitiendo por una sola fila, una espera de 10–40 ms por
    /// intento no separa lo suficiente a los perdedores y varios vuelven a chocar
    /// en la misma ventana. El proveedor InMemory no reproduce el conflicto, así
    /// que el tope se veía holgado y no lo era.
    /// </remarks>
    public const int MaxConcurrencyAttempts = 8;

    /// <summary>Techo de la espera entre intentos, para que un pico no encole segundos.</summary>
    private const int MaxBackoffMilliseconds = 500;

    public async Task<T> ExecuteWithConcurrencyRetryAsync<T>(
        Func<int, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(attempt, cancellationToken);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxConcurrencyAttempts)
            {
                // El tracker quedó con valores obsoletos: se descarta para que el
                // siguiente intento relea el estado real en lugar de reintentar
                // el mismo UPDATE condenado a fallar otra vez.
                ChangeTracker.Clear();

                // Espera exponencial con jitter: sin el jitter, todos los perdedores
                // de la carrera reintentarían a la vez y volverían a chocar; sin el
                // crecimiento exponencial, la ventana no se abre lo bastante rápido
                // cuando los escritores son muchos y el reintento se agota.
                var backoff = Math.Min(
                    Random.Shared.Next(10, 40) * Math.Pow(2, attempt - 1),
                    MaxBackoffMilliseconds);

                await Task.Delay(TimeSpan.FromMilliseconds(backoff), cancellationToken);
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Toma todas las clases IEntityTypeConfiguration<> de este ensamblado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
