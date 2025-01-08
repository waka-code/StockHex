using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UserUseCases
{
    public class GetUserById
    {
        private readonly UserService _userService;

        public GetUserById(UserService userService)
        {
            _userService = userService;
        }

        public async Task<User> RunAsync(Guid id)
        {
            var user = await _userService.GetUserById(id);

            if (user == null)
                throw new KeyNotFoundException($"user with ID not found: {id}");

            return user;
        }
    }
}
