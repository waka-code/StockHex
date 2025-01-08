using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Services;

namespace StockHex_API.Application.UseCases.UserUseCases
{
    public class UpdateUser
    {
        private readonly UserService _userService;

        public UpdateUser(UserService userService)
        {
            _userService = userService;
        }

        public async Task Run(User user)
        {
            var user_exist = await _userService.GetUserById(user.Id);

            if (user_exist == null)
            {
                throw new KeyNotFoundException($"User not found: {user.Id}");
            }

            user_exist.Creation_date = user.Creation_date;
            user_exist.Name = user.Name;
            user_exist.Email = user.Email;
            user_exist.Password = user.Password;
            user_exist.Password_Confirmed = user.Password_Confirmed;
            user_exist.Role = user.Role;
            user_exist.Email_Confirmed = user.Email_Confirmed;

            await _userService.UpdateUserById(user_exist);


        }
    }
}
