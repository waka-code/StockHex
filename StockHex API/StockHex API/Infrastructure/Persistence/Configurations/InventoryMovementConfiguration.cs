using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Infrastructure.Persistence.Configurations;

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("InventoryMovements");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MovementType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(m => m.Quantity).IsRequired();

        builder.Property(m => m.UnitPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(m => m.StockBefore).IsRequired();
        builder.Property(m => m.StockAfter).IsRequired();
        builder.Property(m => m.MovementDate).IsRequired();
        builder.Property(m => m.Comment).HasMaxLength(500);

        // Todas las FK son Restrict: el historial de inventario es un registro de
        // auditoría y no debe desaparecer al borrar un producto, usuario o cliente.
        builder.HasOne(m => m.Product)
            .WithMany(p => p.Movements)
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.User)
            .WithMany(u => u.Movements)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Client)
            .WithMany(c => c.Movements)
            .HasForeignKey(m => m.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Supplier)
            .WithMany(s => s.Movements)
            .HasForeignKey(m => m.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // Auto-referencia: el movimiento de corrección apunta al que corrige.
        builder.HasOne(m => m.ReversalOfMovement)
            .WithMany()
            .HasForeignKey(m => m.ReversalOfMovementId)
            .OnDelete(DeleteBehavior.Restrict);

        // Único: un movimiento no puede revertirse dos veces. La garantía la da la
        // base de datos, no sólo la comprobación previa de la use case.
        builder.HasIndex(m => m.ReversalOfMovementId)
            .IsUnique()
            .HasFilter("[ReversalOfMovementId] IS NOT NULL")
            .HasDatabaseName("IX_InventoryMovements_ReversalOfMovementId");

        builder.HasIndex(m => m.ProductId).HasDatabaseName("IX_InventoryMovements_ProductId");
        builder.HasIndex(m => m.ClientId).HasDatabaseName("IX_InventoryMovements_ClientId");
        builder.HasIndex(m => m.UserId).HasDatabaseName("IX_InventoryMovements_UserId");
        builder.HasIndex(m => m.SupplierId).HasDatabaseName("IX_InventoryMovements_SupplierId");

        // Índice compuesto para el caso más frecuente: historial de un producto por fecha.
        builder.HasIndex(m => new { m.ProductId, m.MovementDate })
            .HasDatabaseName("IX_InventoryMovements_ProductId_MovementDate");

        builder.HasIndex(m => m.MovementDate).HasDatabaseName("IX_InventoryMovements_MovementDate");

        builder.Ignore(m => m.Delta);
        builder.Ignore(m => m.IsReversal);
    }
}
