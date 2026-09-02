namespace StockHex_API.Domain.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Hash BCrypt. Nunca se expone en un DTO de respuesta.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public Guid RoleId { get; set; }

    /// <summary>El rol define qué puede hacer. Se carga cuando hace falta resolver permisos.</summary>
    public Role? Role { get; set; }

    public bool IsActive { get; set; } = true;

    public bool EmailConfirmed { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
