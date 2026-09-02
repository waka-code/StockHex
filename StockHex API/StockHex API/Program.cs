using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using StockHex_API.Api.Extensions;
using StockHex_API.Api.Middleware;
using StockHex_API.Application.DependencyInjection;
using StockHex_API.Infrastructure.DependencyInjection;
using StockHex_API.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- Logging
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// ---------------------------------------------------------------- Servicios
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Los enums viajan como texto ("In", "Admin") en lugar de números:
        // el contrato se lee solo y no se rompe si cambia el orden del enum.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Valida los DTOs antes de que la petición llegue al controlador.
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddSwaggerWithJwt();
builder.Services.AddConfiguredForwardedHeaders(builder.Configuration);
builder.Services.AddConfiguredCors(builder.Configuration);
builder.Services.AddConfiguredRateLimiting(builder.Configuration);
builder.Services.AddPersistence();
builder.Services.AddSecurity(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddBackgroundJobs(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>("database");

// Uniforma las respuestas de errores de binding/validación de MVC con las del middleware.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Detail = "Uno o más campos son inválidos.",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    };
});

var app = builder.Build();

// Falla al arrancar, con un mensaje que dice exactamente qué falta, en lugar de
// quedar en pie devolviendo 500 opacos en cada petición que toque la base.
// Se valida sobre app.Configuration porque ahí ya están todas las fuentes.
InfrastructureServiceCollectionExtensions.RequireConnectionString(app.Configuration);

// ---------------------------------------------------------------- Migraciones
// Se aplican en un Task aparte y con timeout: si la base todavía no responde,
// la API igual queda escuchando y /health/live reporta el estado, en vez de
// quedarse colgada en el bucle de reintentos de EF antes de abrir el puerto.
await ApplyMigrationsAsync(app);

// ---------------------------------------------------------------- Pipeline
// Primero de todo: reescribe RemoteIpAddress con la IP real del cliente, para que
// el rate limiting y los logs no vean la del proxy.
app.UseConfiguredForwardedHeaders();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging();

// Swagger va por configuración, no atado a Development: así el contenedor puede
// exponerlo (Swagger__Enabled=true) sin tener que arrancar con appsettings de desarrollo.
var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "StockHex API");
}

// Sólo se fuerza HTTPS cuando hay un puerto HTTPS configurado; en Docker no lo hay
// y redirigir sin certificado dejaba la API inalcanzable.
if (!string.IsNullOrEmpty(app.Configuration["ASPNETCORE_HTTPS_PORTS"]) ||
    !string.IsNullOrEmpty(app.Configuration["HTTPS_PORT"]))
{
    app.UseHttpsRedirection();
}

app.UseCors(StockHexCorsExtensions.PolicyName);

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    // Liveness: responde si el proceso está en pie, sin tocar la base.
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready");

// La raíz lleva a Swagger si está expuesto; si no, a la comprobación de salud,
// para que no redirija a un 404.
app.MapGet("/", () => Results.Redirect(swaggerEnabled ? "/swagger" : "/health/ready"))
    .ExcludeFromDescription();

app.Run();

// ---------------------------------------------------------------- Helpers
static async Task ApplyMigrationsAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:MigrateOnStartup", true))
        return;

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var timeout = TimeSpan.FromSeconds(
        app.Configuration.GetValue("Database:MigrationTimeoutSeconds", 60));

    using var cts = new CancellationTokenSource(timeout);

    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        logger.LogInformation("Aplicando migraciones pendientes...");
        await context.Database.MigrateAsync(cts.Token);
        logger.LogInformation("Migraciones aplicadas.");

        // Antes de sembrar: el seeder busca a alguien con 'roles.edit' para decidir
        // si hace falta un administrador inicial, y es esta sincronización la que
        // garantiza que el rol de sistema conceda esa clave aunque el catálogo haya
        // crecido desde la migración que lo creó.
        var synchronizer = scope.ServiceProvider.GetRequiredService<PermissionSynchronizer>();
        await synchronizer.SynchronizeAsync(cts.Token);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cts.Token);

        // Datos de demostración para mirar la aplicación en local. No hace nada
        // salvo que Seed:DemoData esté encendido, y viene apagado.
        var demo = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await demo.SeedAsync(cts.Token);
    }
    catch (OperationCanceledException)
    {
        logger.LogError(
            "Las migraciones no terminaron en {Timeout}s. La API arranca igualmente; " +
            "consulta /health/ready para ver el estado de la base.",
            timeout.TotalSeconds);
    }
    catch (Exception ex)
    {
        logger.LogError(ex,
            "Falló la aplicación de migraciones. La API arranca igualmente; " +
            "consulta /health/ready para ver el estado de la base.");
    }
}

// ---------------------------------------------------------------- Puntos de extensión
/// <summary>
/// Hace visible el punto de entrada para <c>WebApplicationFactory&lt;Program&gt;</c>,
/// que es lo que permite a los tests de integración levantar la API real.
/// </summary>
public partial class Program;
