using FluentAssertions;
using Microsoft.Extensions.Configuration;
using StockHex_API.Infrastructure.DependencyInjection;

namespace StockHex_API.Tests.UseCases;

/// <summary>
/// La configuración tiene que fallar con un mensaje claro, no dejar la API en pie
/// devolviendo 500 opacos en cada petición que toque la base.
/// </summary>
public sealed class ConfigurationGuardTests
{
    private static IConfiguration Build(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString
            })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Una_cadena_de_conexion_ausente_o_vacia_se_rechaza(string? connectionString)
    {
        // appsettings.json deja la clave presente pero vacía, así que comprobar
        // sólo por null dejaría pasar el caso más probable de un despliegue nuevo.
        var act = () => InfrastructureServiceCollectionExtensions
            .RequireConnectionString(Build(connectionString));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ConnectionStrings:DefaultConnection*");
    }

    [Fact]
    public void Una_cadena_de_conexion_valida_se_devuelve_tal_cual()
    {
        const string expected = "Server=(local);Database=StockHex;Trusted_Connection=True;";

        InfrastructureServiceCollectionExtensions
            .RequireConnectionString(Build(expected))
            .Should().Be(expected);
    }
}
