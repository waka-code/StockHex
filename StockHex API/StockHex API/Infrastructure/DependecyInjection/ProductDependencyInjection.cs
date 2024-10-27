using StockHex_API.Application.UseCases.ProductUseCases;
using StockHex_API.Application.UseCases.UseProduct;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Domain.Services;
using StockHex_API.Infrastructure.Repositories;

namespace StockHex_API.Infrastructure.DependecyInjection
{
    public static class ProductDependencyInjection
    {
        public static IServiceCollection AddProductoServices(this IServiceCollection services)
        {
            services.AddScoped<IProductRepository, ProductRepository>();
            services.AddScoped<ProductService>();
            services.AddScoped<PostProduct>();
            services.AddScoped<GetProductById>();
            services.AddScoped<GetAllProducts>();
            services.AddScoped<UpdateProductById>();
            services.AddScoped<DeleteProductById>();

            return services;
        }
    }
}
