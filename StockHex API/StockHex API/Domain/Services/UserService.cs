using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using System.Security.Cryptography;

namespace StockHex_API.Domain.Services
{
    public class UserService : IUserRepository
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository repository)
        {
            _userRepository = repository;
        }

        public async Task DeleteUserById(Guid id)
        {

            var delete = await _userRepository.GetUserById(id);
            if(delete == null)
            {
                throw new KeyNotFoundException($"User Not Found: {id}");
            }
           
            await _userRepository.DeleteUserById(id);
        }

        public async Task<IEnumerable<User>> GetAllUser()
        {
            return await _userRepository.GetAllUser();
        }

        public async Task<User> GetUserById(Guid id)
        {
            var user = await _userRepository.GetUserById(id);
            if (user == null)
                throw new KeyNotFoundException($"User Not Found: {id}");
            return user;
        }

        public async Task<User> PostUser(User user)
        {

            if (user == null)
                throw new ArgumentNullException(nameof(user), "User Not Found.");

            var existingUser = await _userRepository.GetAllUser();

            if (existingUser.Any(u => u.Email == user.Email))
            {
                throw new InvalidOperationException("Email Exist.");
            }

            var salt = new byte[128 / 8]; 
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var hashedPassword = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: user.Password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            user.Password = hashedPassword;
            return await _userRepository.PostUser(user);
        }

        public async Task<User> UpdateUserById(User user)
        {
            var userExist = await _userRepository.GetUserById(user.Id);
            if(userExist == null)
            {
                throw new KeyNotFoundException($"User with ID not found: {user.Id}");
            }
            userExist.Name = user.Name;
            userExist.Email = user.Email;
            userExist.Password = user.Password;
            userExist.Email_Confirmed = user.Email_Confirmed;
            userExist.Password_Confirmed = user.Password_Confirmed;
            userExist.Role = user.Role;
            userExist.Creation_date = user.Creation_date;
            return await _userRepository.UpdateUserById(userExist);
        }
    }
}
