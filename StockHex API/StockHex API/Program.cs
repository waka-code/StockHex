using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StockHex_API.Infrastructure.DependecyInjection;
using StockHex_API.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuración de la cadena de conexión y registro de ApplicationDbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure()
    )
);

//var config = builder.Configuration;
//var defaultConnection = config.GetConnectionString("DefaultConnection");

//Console.WriteLine($"Connection String: {defaultConnection}");

// Registro del repositorio
builder.Services.AddProductoServices();
builder.Services.AddCategoryServices();

var app = builder.Build();

// Ejecutar migraciones al inicio para crear la BD si no existe
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        logger.LogInformation("Applying migrations (if any)...");
        db.Database.Migrate();
        logger.LogInformation("Migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while applying migrations.");
        // If migrations fail, we keep the app running but the error will be visible in logs
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseCors("AllowAngularApp");
// No forzar HTTPS en producción/Docker si no hay certificado configurado
// app.UseHttpsRedirection();
app.UseAuthorization();

// Redirect root to Swagger UI so http://host:port/ opens Swagger
app.MapGet("/", context =>
{
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.MapControllers();
app.Run();