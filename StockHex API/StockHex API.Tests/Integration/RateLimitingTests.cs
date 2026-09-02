using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using StockHex_API.Application.DTOs;

namespace StockHex_API.Tests.Integration;

/// <summary>Fábrica con un límite deliberadamente bajo para poder observar el 429.</summary>
public sealed class RateLimitedApiFactory : StockHexApiFactory
{
    public const int PermitLimit = 3;

    protected override IReadOnlyDictionary<string, string?> ConfigurationOverrides =>
        new Dictionary<string, string?>
        {
            ["RateLimiting:AuthPermitLimit"] = PermitLimit.ToString(),
            ["RateLimiting:AuthWindowSeconds"] = "60"
        };
}

/// <summary>
/// Cada test crea su propia fábrica en lugar de compartirla con <c>IClassFixture</c>:
/// la ventana del limitador es estado compartido, y con una fábrica común el
/// presupuesto de un test se consumiría en el anterior.
/// </summary>
public sealed class RateLimitingTests
{
    private static async Task<(RateLimitedApiFactory Factory, HttpClient Client)> CreateAsync()
    {
        var factory = new RateLimitedApiFactory();
        await factory.SeedUsersAsync();
        return (factory, factory.CreateClient());
    }

    private static LoginRequest BadCredentials =>
        new(StockHexApiFactory.AdminEmail, "incorrecta");

    [Fact]
    public async Task Los_intentos_de_login_por_encima_del_limite_devuelven_429()
    {
        var (factory, client) = await CreateAsync();
        using var _ = factory;

        var statuses = new List<HttpStatusCode>();

        // Una petición más que el límite: las primeras se rechazan por credenciales,
        // la última la corta el limitador antes de comprobarlas.
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit + 1; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", BadCredentials);
            statuses.Add(response.StatusCode);
        }

        statuses.Take(RateLimitedApiFactory.PermitLimit)
            .Should().AllBeEquivalentTo(HttpStatusCode.Unauthorized,
                "dentro del límite la petición llega a validar credenciales");

        statuses.Last().Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task El_429_responde_en_formato_problem_details_con_Retry_After()
    {
        var (factory, client) = await CreateAsync();
        using var _ = factory;

        HttpResponseMessage? limited = null;
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit + 2 && limited is null; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", BadCredentials);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                limited = response;
        }

        limited.Should().NotBeNull("con el límite en 3 el limitador debe activarse");
        limited!.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        limited.Headers.RetryAfter.Should().NotBeNull("el cliente necesita saber cuánto esperar");

        var body = await limited.Content.ReadAsStringAsync();
        body.Should().Contain("rate_limited");
    }

    [Fact]
    public async Task Un_login_correcto_tambien_consume_presupuesto()
    {
        var (factory, client) = await CreateAsync();
        using var _ = factory;

        // El límite protege el endpoint entero, no sólo los intentos fallidos:
        // si no, bastaría alternar con credenciales válidas para eludirlo.
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            var ok = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword));
            ok.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword));

        blocked.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task El_limite_no_afecta_a_los_endpoints_que_no_son_de_autenticacion()
    {
        var (factory, client) = await CreateAsync();
        using var _ = factory;

        // Muchas más peticiones que el límite de /api/auth, sobre una ruta protegida.
        for (var i = 0; i < RateLimitedApiFactory.PermitLimit * 3; i++)
        {
            var response = await client.GetAsync("/api/products");
            // 401 por falta de token, nunca 429: la política sólo cubre /api/auth.
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
