
using StockHex_API.Application.UseCases.UserUseCases;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Domain.Services;
using StockHex_API.Infrastructure.Repositories;

namespace StockHex_API.Infrastructure.DependecyInjection
{
    public static class UserDependencyInjection
    {
        public static IServiceCollection AddUserServices(this IServiceCollection services)
        {
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<UserService>();
            services.AddScoped<PostUser>();
            services.AddScoped<GetUserById>();
            services.AddScoped<GetAllUser>();
            services.AddScoped<UpdateUser>();
            services.AddScoped<DeleteUserById>();

            return services;
        }
    }
}
