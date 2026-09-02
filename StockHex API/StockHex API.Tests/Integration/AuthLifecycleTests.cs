using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Tests.Integration;

/// <summary>Ciclo de vida de la sesión por HTTP: login, renovación, reversión y cierre.</summary>
public sealed class AuthLifecycleTests : IClassFixture<StockHexApiFactory>, IAsyncLifetime
{
    private readonly StockHexApiFactory _factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public AuthLifecycleTests(StockHexApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.SeedUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpClient Client, AuthResponse Auth)> LoginAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword), Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, auth);
    }

    // ------------------------------------------------------------ Renovación

    [Fact]
    public async Task El_login_devuelve_access_y_refresh_token()
    {
        var (_, auth) = await LoginAsync();

        auth.AccessToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
        auth.RefreshTokenExpiresAt.Should().BeAfter(auth.ExpiresAt);
    }

    [Fact]
    public async Task El_refresh_entrega_un_access_token_usable()
    {
        var (_, auth) = await LoginAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken), Json);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        renewed.RefreshToken.Should().NotBe(auth.RefreshToken, "el token rota en cada canje");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reutilizar_el_refresh_token_ya_canjeado_devuelve_401()
    {
        var (_, auth) = await LoginAsync();
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken), Json))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var reused = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken), Json);

        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Un_refresh_token_invalido_devuelve_401_no_500()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest("token-que-no-existe"), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Un_refresh_vacio_devuelve_400_por_validacion()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(""), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------ Cierre de sesión

    [Fact]
    public async Task El_logout_deja_el_refresh_token_inservible()
    {
        var (client, auth) = await LoginAsync();

        var logout = await client.PostAsJsonAsync("/api/auth/logout",
            new LogoutRequest(auth.RefreshToken), Json);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterLogout = await _factory.CreateClient().PostAsJsonAsync("/api/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken), Json);

        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task El_logout_requiere_estar_autenticado()
    {
        var (_, auth) = await LoginAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/logout",
            new LogoutRequest(auth.RefreshToken), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ------------------------------------------------------------ Reversión

    [Fact]
    public async Task Se_puede_revertir_un_movimiento_por_HTTP_y_el_stock_vuelve_atras()
    {
        var (client, _) = await LoginAsync();

        var category = (await (await client.PostAsJsonAsync("/api/categories",
                new CreateCategoryRequest($"Cat {Guid.NewGuid():N}", null), Json))
            .Content.ReadFromJsonAsync<CategoryResponse>(Json))!;

        var product = (await (await client.PostAsJsonAsync("/api/products",
                new CreateProductRequest("Producto", null, $"R{Guid.NewGuid():N}"[..10],
                    100m, 0, category.Id, null), Json))
            .Content.ReadFromJsonAsync<ProductResponse>(Json))!;

        var movement = (await (await client.PostAsJsonAsync("/api/inventory-movements",
                new CreateMovementRequest(product.Id, MovementType.In, 60, 10m, null, null, "Compra"), Json))
            .Content.ReadFromJsonAsync<MovementResponse>(Json))!;
        movement.StockAfter.Should().Be(60);

        var reversal = await client.PostAsJsonAsync($"/api/inventory-movements/{movement.Id}/reverse",
            new ReverseMovementRequest("Compra anulada"), Json);

        reversal.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = (await reversal.Content.ReadFromJsonAsync<MovementResponse>(Json))!;
        body.MovementType.Should().Be(MovementType.Out);
        body.ReversalOfMovementId.Should().Be(movement.Id);
        body.StockAfter.Should().Be(0);

        var refreshed = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", Json);
        refreshed!.StockQuantity.Should().Be(0);

        // Segunda reversión del mismo movimiento: 409.
        (await client.PostAsJsonAsync($"/api/inventory-movements/{movement.Id}/reverse",
            new ReverseMovementRequest(null), Json))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Un_operador_no_puede_revertir_movimientos()
    {
        var (admin, _) = await LoginAsync();

        var category = (await (await admin.PostAsJsonAsync("/api/categories",
                new CreateCategoryRequest($"Cat {Guid.NewGuid():N}", null), Json))
            .Content.ReadFromJsonAsync<CategoryResponse>(Json))!;

        var product = (await (await admin.PostAsJsonAsync("/api/products",
                new CreateProductRequest("Producto", null, $"O{Guid.NewGuid():N}"[..10],
                    100m, 0, category.Id, null), Json))
            .Content.ReadFromJsonAsync<ProductResponse>(Json))!;

        var movement = (await (await admin.PostAsJsonAsync("/api/inventory-movements",
                new CreateMovementRequest(product.Id, MovementType.In, 5, null, null, null, null), Json))
            .Content.ReadFromJsonAsync<MovementResponse>(Json))!;

        var operatorClient = _factory.CreateClient();
        var operatorAuth = (await (await operatorClient.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(StockHexApiFactory.OperatorEmail, StockHexApiFactory.OperatorPassword), Json))
            .Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        operatorClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", operatorAuth.AccessToken);

        // Registrar movimientos sí, corregir el historial no.
        var response = await operatorClient.PostAsJsonAsync(
            $"/api/inventory-movements/{movement.Id}/reverse",
            new ReverseMovementRequest(null), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task El_historial_se_puede_filtrar_por_proveedor()
    {
        var (client, _) = await LoginAsync();

        var category = (await (await client.PostAsJsonAsync("/api/categories",
                new CreateCategoryRequest($"Cat {Guid.NewGuid():N}", null), Json))
            .Content.ReadFromJsonAsync<CategoryResponse>(Json))!;

        var supplier = (await (await client.PostAsJsonAsync("/api/suppliers",
                new CreateSupplierRequest($"Prov {Guid.NewGuid():N}", null, null, null), Json))
            .Content.ReadFromJsonAsync<SupplierResponse>(Json))!;

        var product = (await (await client.PostAsJsonAsync("/api/products",
                new CreateProductRequest("Producto", null, $"S{Guid.NewGuid():N}"[..10],
                    100m, 0, category.Id, null), Json))
            .Content.ReadFromJsonAsync<ProductResponse>(Json))!;

        await client.PostAsJsonAsync("/api/inventory-movements",
            new CreateMovementRequest(product.Id, MovementType.In, 25, 40m, null, supplier.Id, "Compra"), Json);

        var page = await client.GetFromJsonAsync<PagedResponse<MovementResponse>>(
            $"/api/inventory-movements?supplierId={supplier.Id}", Json);

        page!.Items.Should().HaveCount(1);
        page.Items[0].SupplierName.Should().Be(supplier.Name);
    }
}
