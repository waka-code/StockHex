using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UserUseCases
{
    public class DeleteUserById
    {
        private readonly UserService _userService;

        public DeleteUserById(UserService userService)
        {
            _userService = userService;
        }

        public async Task Run(Guid id)
        {
            var user = await _userService.GetUserById(id);

            if (user == null)
                throw new KeyNotFoundException($"user with ID not found: {id}");

            await _userService.DeleteUserById(id);

        }

    }
}
