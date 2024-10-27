using StockHex_API.Application.UseCases.CategoryUseCases;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Domain.Services;
using StockHex_API.Infrastructure.Repositories;

namespace StockHex_API.Infrastructure.DependecyInjection
{
    public static class CategoryDependencyInjection
    {
        public static IServiceCollection AddCategoryServices(this IServiceCollection services)
        {
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<CategoryService>();
            services.AddScoped<PostCategory>();
            services.AddScoped<GetCategoryById>();
            services.AddScoped<GetAllCategory>();
            services.AddScoped<UpdateCategoryById>();
            services.AddScoped<DeleteCategoryById>();

            return services;
        }
    }
}
