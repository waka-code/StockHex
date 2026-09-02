using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockHex_API.Domain.Entities;

namespace StockHex_API.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(80);

        builder.Property(r => r.Description).HasMaxLength(300);
        builder.Property(r => r.IsSystem).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasIndex(r => r.Name)
            .IsUnique()
            .HasDatabaseName("IX_Roles_Name");

        builder.Ignore(r => r.PermissionKeys);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");

        builder.HasKey(p => p.Id);

        // Sin FK a una tabla de permisos: el catálogo vive en el código (regla 7).
        // La clave se valida contra Permissions.All antes de escribir.
        builder.Property(p => p.Permission)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasOne(p => p.Role)
            .WithMany(r => r.Permissions)
            .HasForeignKey(p => p.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un rol no puede conceder el mismo permiso dos veces.
        builder.HasIndex(p => new { p.RoleId, p.Permission })
            .IsUnique()
            .HasDatabaseName("IX_RolePermissions_RoleId_Permission");
    }
}
