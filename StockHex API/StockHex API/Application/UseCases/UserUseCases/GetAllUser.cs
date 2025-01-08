using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UserUseCases
{
    public class GetAllUser
    {
        private readonly UserService _userService;

        public GetAllUser(UserService userService)
        {
            _userService = userService;
        }

        public async Task<IEnumerable<User>> Run()
        {
            return await _userService.GetAllUser();
        }
    }
}
