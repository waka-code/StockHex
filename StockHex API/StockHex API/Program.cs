using Microsoft.EntityFrameworkCore;
using StockHex_API.Infrastructure.DependecyInjection;
using StockHex_API.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuración de la cadena de conexión y registro de ApplicationDbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

//var config = builder.Configuration;
//var defaultConnection = config.GetConnectionString("DefaultConnection");
//Console.WriteLine($"Connection String: {defaultConnection}");

// Registro del repositorio
builder.Services.AddProductoServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngularApp");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();