using StockHex_API.Domain.Entities;

namespace StockHex_API.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User> GetUserById(Guid id);
        Task<IEnumerable<User>> GetAllUser();
        Task<User> PostUser(User user);
        Task<User> UpdateUserById(User user);
        Task DeleteUserById(Guid id);
    }
}
