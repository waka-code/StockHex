using FluentAssertions;
using StockHex_API.Infrastructure.Security;

namespace StockHex_API.Tests.Security;

/// <summary>
/// El hasher anterior generaba un salt aleatorio, lo descartaba y guardaba sólo el
/// hash, por lo que ninguna contraseña podía verificarse. Estos tests fijan ese contrato.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Una_contrasena_hasheada_se_puede_verificar()
    {
        var hash = _hasher.Hash("Password123");

        _hasher.Verify("Password123", hash).Should().BeTrue();
    }

    [Fact]
    public void Una_contrasena_incorrecta_no_verifica()
    {
        var hash = _hasher.Hash("Password123");

        _hasher.Verify("Password124", hash).Should().BeFalse();
    }

    [Fact]
    public void Dos_hashes_de_la_misma_contrasena_son_distintos()
    {
        // BCrypt incluye un salt distinto en cada hash, así que dos usuarios con la
        // misma contraseña no comparten hash.
        _hasher.Hash("Password123").Should().NotBe(_hasher.Hash("Password123"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-hash")]
    [InlineData("$2a$roto")]
    public void Un_hash_con_formato_invalido_se_trata_como_credencial_incorrecta(string hash)
    {
        _hasher.Verify("Password123", hash).Should().BeFalse();
    }
}
