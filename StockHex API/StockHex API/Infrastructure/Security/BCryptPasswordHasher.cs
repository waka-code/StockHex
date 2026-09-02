using StockHex_API.Application.Abstractions;

namespace StockHex_API.Infrastructure.Security;

/// <summary>
/// Hashing con BCrypt. Reemplaza al PBKDF2 anterior, que generaba un salt aleatorio
/// y lo descartaba, con lo que ninguna contraseña podía verificarse después.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    /// <summary>Coste de trabajo; 12 es el equilibrio recomendado hoy entre seguridad y latencia.</summary>
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(hash))
            return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash con formato inválido (por ejemplo, escrito por una versión anterior):
            // se trata como credencial incorrecta en lugar de propagar la excepción.
            return false;
        }
    }
}
