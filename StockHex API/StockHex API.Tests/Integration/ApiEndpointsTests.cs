using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using StockHex_API.Application.DTOs;
using StockHex_API.Domain.Enums;

namespace StockHex_API.Tests.Integration;

public sealed class ApiEndpointsTests : IClassFixture<StockHexApiFactory>, IAsyncLifetime
{
    private readonly StockHexApiFactory _factory;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ApiEndpointsTests(StockHexApiFactory factory) => _factory = factory;

    public Task InitializeAsync() => _factory.SeedUsersAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "el login de los usuarios sembrados debe funcionar");

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    // ------------------------------------------------------------ Autenticación

    [Fact]
    public async Task Un_endpoint_protegido_sin_token_devuelve_401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_con_credenciales_incorrectas_devuelve_401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(StockHexApiFactory.AdminEmail, "incorrecta"), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_valido_devuelve_un_token_usable()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<UserResponse>(Json);
        me!.Email.Should().Be(StockHexApiFactory.AdminEmail);
        me.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public async Task La_respuesta_de_usuario_no_expone_el_hash_de_la_contrasena()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var body = await client.GetStringAsync("/api/auth/me");

        // Antes se serializaba la entidad completa, con el hash incluido.
        body.Should().NotContain("passwordHash");
        body.ToLowerInvariant().Should().NotContain("password");
    }

    // ------------------------------------------------------------ Autorización por rol

    [Fact]
    public async Task Un_operador_no_puede_crear_categorias()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.OperatorEmail, StockHexApiFactory.OperatorPassword);

        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("Prohibida", null), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_operador_no_puede_listar_usuarios()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.OperatorEmail, StockHexApiFactory.OperatorPassword);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_administrador_si_puede_listar_usuarios()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ------------------------------------------------------------ Validación y errores

    [Fact]
    public async Task Un_body_invalido_devuelve_400_con_problem_details()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var response = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest("", null), Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("obligatorio");
    }

    [Fact]
    public async Task Un_id_inexistente_devuelve_404_y_no_500()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}");

        // Antes del middleware de errores, un KeyNotFoundException salía como 500.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    [Fact]
    public async Task Un_guid_mal_formado_en_la_ruta_no_matchea_el_endpoint()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var response = await client.GetAsync("/api/products/no-es-un-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ------------------------------------------------------------ Flujo completo

    [Fact]
    public async Task Flujo_completo_categoria_producto_entrada_y_salida()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        // 1. Categoría
        var categoryResponse = await client.PostAsJsonAsync("/api/categories",
            new CreateCategoryRequest($"Bebidas {Guid.NewGuid():N}", "Líquidos"), Json);
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        categoryResponse.Headers.Location.Should().NotBeNull("un 201 debe indicar dónde quedó el recurso");
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponse>(Json);

        // 2. Producto: nace con stock en cero
        var productResponse = await client.PostAsJsonAsync("/api/products",
            new CreateProductRequest("Agua 500ml", null, $"AGUA-{Guid.NewGuid():N}"[..12],
                990m, 10, category!.Id, null), Json);
        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>(Json);
        product!.StockQuantity.Should().Be(0);

        // 3. Entrada de 100 unidades
        var inResponse = await client.PostAsJsonAsync("/api/inventory-movements",
            new CreateMovementRequest(product.Id, MovementType.In, 100, 500m, null, null, "Compra inicial"), Json);
        inResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var inMovement = await inResponse.Content.ReadFromJsonAsync<MovementResponse>(Json);
        inMovement!.StockAfter.Should().Be(100);

        // 4. Salida de 30
        var outResponse = await client.PostAsJsonAsync("/api/inventory-movements",
            new CreateMovementRequest(product.Id, MovementType.Out, 30, null, null, null, "Venta"), Json);
        outResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        (await outResponse.Content.ReadFromJsonAsync<MovementResponse>(Json))!.StockAfter.Should().Be(70);

        // 5. Salida por encima del stock: 409, sin efecto
        var overResponse = await client.PostAsJsonAsync("/api/inventory-movements",
            new CreateMovementRequest(product.Id, MovementType.Out, 1_000, null, null, null, null), Json);
        overResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // 6. El stock final refleja sólo los movimientos válidos
        var finalProduct = await client.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", Json);
        finalProduct!.StockQuantity.Should().Be(70);

        // 7. El historial quedó registrado
        var history = await client.GetFromJsonAsync<PagedResponse<MovementResponse>>(
            $"/api/inventory-movements?productId={product.Id}", Json);
        history!.Items.Should().HaveCount(2);
        history.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Un_operador_si_puede_registrar_movimientos()
    {
        var admin = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var category = await (await admin.PostAsJsonAsync("/api/categories",
                new CreateCategoryRequest($"Cat {Guid.NewGuid():N}", null), Json))
            .Content.ReadFromJsonAsync<CategoryResponse>(Json);

        var product = await (await admin.PostAsJsonAsync("/api/products",
                new CreateProductRequest("Producto", null, $"P{Guid.NewGuid():N}"[..10],
                    100m, 0, category!.Id, null), Json))
            .Content.ReadFromJsonAsync<ProductResponse>(Json);

        var operatorClient = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.OperatorEmail, StockHexApiFactory.OperatorPassword);

        var response = await operatorClient.PostAsJsonAsync("/api/inventory-movements",
            new CreateMovementRequest(product!.Id, MovementType.In, 5, null, null, null, null), Json);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // El movimiento se atribuye al operador, no a quien creó el producto.
        var movement = await response.Content.ReadFromJsonAsync<MovementResponse>(Json);
        movement!.UserName.Should().Be("Operador");
    }

    [Fact]
    public async Task La_paginacion_acota_el_tamano_de_pagina_solicitado()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var page = await client.GetFromJsonAsync<PagedResponse<UserResponse>>(
            "/api/users?page=1&pageSize=100000", Json);

        page!.PageSize.Should().Be(100, "pageSize se acota a PageRequest.MaxPageSize");
    }

    [Fact]
    public async Task El_reporte_de_resumen_responde_con_los_totales()
    {
        var client = await CreateAuthenticatedClientAsync(
            StockHexApiFactory.AdminEmail, StockHexApiFactory.AdminPassword);

        var response = await client.GetAsync("/api/reports/inventory-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<InventorySummaryResponse>(Json);
        summary!.TotalProducts.Should().BeGreaterThanOrEqualTo(0);
    }

    // ------------------------------------------------------------ Salud

    [Fact]
    public async Task El_endpoint_de_liveness_responde_sin_tocar_la_base()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
