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
    private const int MaxConcurrencyAttempts = 5;

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

                // Espera con jitter: sin él, todos los perdedores de la carrera
                // reintentarían a la vez y volverían a chocar.
                var backoff = TimeSpan.FromMilliseconds(
                    Random.Shared.Next(10, 40) * attempt);

                await Task.Delay(backoff, cancellationToken);
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
