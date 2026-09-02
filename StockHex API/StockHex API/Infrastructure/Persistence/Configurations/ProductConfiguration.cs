using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Infrastructure.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(p => p.Description).HasMaxLength(500);

        builder.Property(p => p.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.StockQuantity).IsRequired();
        builder.Property(p => p.MinimumStock).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();

        // Detecta escrituras concurrentes sobre el mismo producto: dos movimientos
        // simultáneos no pueden pisarse el stock silenciosamente.
        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        builder.Property(p => p.Sku)
            .UseCollation("SQL_Latin1_General_CP1_CS_AS");

        builder.HasIndex(p => p.Sku)
            .IsUnique()
            .HasDatabaseName("IX_Products_Sku");

        builder.HasIndex(p => p.CategoryId).HasDatabaseName("IX_Products_CategoryId");
        builder.HasIndex(p => p.SupplierId).HasDatabaseName("IX_Products_SupplierId");

        // Restrict en lugar de Cascade: borrar una categoría no debe arrastrar productos.
        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Supplier)
            .WithMany(s => s.Products)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Ignore(p => p.IsLowStock);
    }
}
