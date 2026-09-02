using System.ComponentModel.DataAnnotations;

namespace StockHex_API.Infrastructure.Security;

/// <summary>
/// Configuración del JWT, validada al arrancar: si falta la clave o es demasiado
/// corta, la aplicación no levanta en lugar de emitir tokens inseguros.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Mínimo exigido por HMAC-SHA256 (256 bits).</summary>
    public const int MinimumKeyLength = 32;

    [Required(ErrorMessage = "Jwt:Issuer es obligatorio.")]
    public string Issuer { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Audience es obligatorio.")]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jwt:Key es obligatorio. Configúralo por variable de entorno Jwt__Key.")]
    [MinLength(MinimumKeyLength,
        ErrorMessage = "Jwt:Key debe tener al menos 32 caracteres para firmar con HMAC-SHA256.")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440, ErrorMessage = "Jwt:AccessTokenMinutes debe estar entre 1 y 1440.")]
    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>
    /// Vigencia del token de refresco. Es larga a propósito: el access token dura
    /// poco y se renueva con éste, de modo que el usuario no vuelve a autenticarse
    /// mientras la sesión siga activa.
    /// </summary>
    [Range(1, 365, ErrorMessage = "Jwt:RefreshTokenDays debe estar entre 1 y 365.")]
    public int RefreshTokenDays { get; set; } = 14;
}
