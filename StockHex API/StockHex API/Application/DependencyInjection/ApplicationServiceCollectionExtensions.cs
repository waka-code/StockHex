using FluentValidation;
using StockHex_API.Application.UseCases.AuthUseCases;
using StockHex_API.Application.UseCases.CategoryUseCases;
using StockHex_API.Application.UseCases.ClientUseCases;
using StockHex_API.Application.UseCases.InventoryMovementUseCases;
using StockHex_API.Application.UseCases.ProductUseCases;
using StockHex_API.Application.UseCases.ReportUseCases;
using StockHex_API.Application.UseCases.RoleUseCases;
using StockHex_API.Application.UseCases.SupplierUseCases;
using StockHex_API.Application.UseCases.UserUseCases;

namespace StockHex_API.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registra las use cases y los validadores. Todas las use cases se registran
    /// aquí, en un solo lugar, para que no vuelva a pasar que un controlador
    /// compile pero falle al resolverse en tiempo de ejecución.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Auth
        services.AddScoped<IssueTokens>();
        services.AddScoped<Login>();
        services.AddScoped<Register>();
        services.AddScoped<RefreshAccessToken>();
        services.AddScoped<Logout>();
        services.AddScoped<GetCurrentUser>();

        // Categories
        services.AddScoped<CreateCategory>();
        services.AddScoped<UpdateCategory>();
        services.AddScoped<DeleteCategory>();
        services.AddScoped<GetCategoryById>();
        services.AddScoped<GetCategories>();

        // Suppliers
        services.AddScoped<CreateSupplier>();
        services.AddScoped<UpdateSupplier>();
        services.AddScoped<DeleteSupplier>();
        services.AddScoped<GetSupplierById>();
        services.AddScoped<GetSuppliers>();

        // Clients
        services.AddScoped<CreateClient>();
        services.AddScoped<UpdateClient>();
        services.AddScoped<DeleteClient>();
        services.AddScoped<GetClientById>();
        services.AddScoped<GetClients>();

        // Products
        services.AddScoped<CreateProduct>();
        services.AddScoped<UpdateProduct>();
        services.AddScoped<DeleteProduct>();
        services.AddScoped<GetProductById>();
        services.AddScoped<GetProducts>();

        // Users
        services.AddScoped<CreateUser>();
        services.AddScoped<UpdateUser>();
        services.AddScoped<DeleteUser>();
        services.AddScoped<GetUserById>();
        services.AddScoped<GetUsers>();
        services.AddScoped<ChangePassword>();
        services.AddScoped<ResetUserPassword>();

        // Roles y permisos
        services.AddScoped<GetPermissionCatalog>();
        services.AddScoped<GetRoles>();
        services.AddScoped<GetRoleById>();
        services.AddScoped<CreateRole>();
        services.AddScoped<UpdateRole>();
        services.AddScoped<DeleteRole>();

        // Inventory movements
        services.AddScoped<CreateMovement>();
        services.AddScoped<ReverseMovement>();
        services.AddScoped<GetMovements>();
        services.AddScoped<GetMovementById>();

        // Reports
        services.AddScoped<GetInventorySummary>();
        services.AddScoped<GetLowStockReport>();
        services.AddScoped<GetMovementSummary>();

        // Todos los AbstractValidator<> de este ensamblado.
        services.AddValidatorsFromAssemblyContaining<Validators.LoginRequestValidator>();

        return services;
    }
}
