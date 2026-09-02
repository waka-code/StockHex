namespace StockHex_API.Domain.Entities;

/// <summary>
/// Token de refresco persistido. Se guarda sólo el hash: si la base se filtra,
/// los tokens no son utilizables. Como son cadenas aleatorias de alta entropía
/// basta SHA-256, no hace falta un hash lento como el de las contraseñas.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SHA-256 en Base64 del token entregado al cliente.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null mientras el token siga vigente.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Por qué se revocó: rotación, cierre de sesión o reutilización detectada.</summary>
    public string? RevokedReason { get; set; }

    /// <summary>Token que lo sustituyó al rotar. Encadena la sesión para poder invalidarla completa.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTime now, string reason)
    {
        RevokedAt = now;
        RevokedReason = reason;
    }
}

/// <summary>Motivos de revocación, para que el campo no se llene con texto libre.</summary>
public static class RevocationReasons
{
    public const string Rotated = "rotated";
    public const string LoggedOut = "logged_out";
    public const string Reused = "reused";
    public const string UserDisabled = "user_disabled";

    /// <summary>El dueño cambió su contraseña: las sesiones anteriores dejan de valer.</summary>
    public const string PasswordChanged = "password_changed";
}
