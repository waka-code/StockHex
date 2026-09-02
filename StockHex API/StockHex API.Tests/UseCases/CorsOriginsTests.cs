using FluentAssertions;
using Microsoft.Extensions.Configuration;
using StockHex_API.Api.Extensions;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// Los orígenes se pueden declarar como arreglo en appsettings o como una sola
/// cadena separada por ';'. La segunda forma existe porque pasar un arreglo por
/// variables de entorno obliga a una variable por elemento, y eso ya dejó fuera
/// al puerto del frontend una vez.
/// </summary>
public sealed class CorsOriginsTests
{
    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Se_leen_los_origenes_declarados_como_arreglo()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
            ["Cors:AllowedOrigins:1"] = "http://localhost:4200",
        });

        StockHexCorsExtensions.ReadOrigins(configuration)
            .Should().Equal("http://localhost:5173", "http://localhost:4200");
    }

    [Theory]
    [InlineData("http://a.cl;http://b.cl")]
    [InlineData("http://a.cl,http://b.cl")]
    [InlineData(" http://a.cl ; http://b.cl ")]
    public void Se_leen_los_origenes_declarados_como_una_sola_cadena(string value)
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = value,
        });

        StockHexCorsExtensions.ReadOrigins(configuration)
            .Should().Equal("http://a.cl", "http://b.cl");
    }

    [Fact]
    public void La_barra_final_se_descarta_porque_el_navegador_no_la_envia()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "http://localhost:5173/",
        });

        // El header Origin nunca trae barra final: dejarla haría que no coincidiera.
        StockHexCorsExtensions.ReadOrigins(configuration)
            .Should().Equal("http://localhost:5173");
    }

    [Fact]
    public void Los_duplicados_se_descartan()
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = "http://a.cl;http://A.CL;http://a.cl/",
        });

        StockHexCorsExtensions.ReadOrigins(configuration).Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(";;")]
    public void Sin_origenes_declarados_devuelve_una_lista_vacia(string? value)
    {
        var configuration = Build(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins"] = value,
        });

        // Lista vacía significa "permitir cualquiera sin credenciales".
        StockHexCorsExtensions.ReadOrigins(configuration).Should().BeEmpty();
    }
}
