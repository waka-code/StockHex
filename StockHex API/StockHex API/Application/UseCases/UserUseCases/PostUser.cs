using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UserUseCases
{
    public class PostUser
    {
        private readonly UserService _userService;

        public PostUser(UserService userService)
        {
            _userService = userService;
        }

        public async Task Run(User user)
        {
            if (string.IsNullOrEmpty(user.Name))
                throw new ArgumentException("The user name is required.");

            if (string.IsNullOrEmpty(user.Email))
                throw new ArgumentException("The Email is required.");

            await _userService.PostUser(user);
        }
    }
}
