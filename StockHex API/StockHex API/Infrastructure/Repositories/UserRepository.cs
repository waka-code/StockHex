using Microsoft.EntityFrameworkCore;
using StockHex_API.Domain.Entities;
using StockHex_API.Domain.Interfaces;
using StockHex_API.Infrastructure.Persistence;

namespace StockHex_API.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task DeleteUserById(Guid id)
        {
            var user = await _context.User.FindAsync(id);
            if (user != null)
            {
                _context.User.Remove(user);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<User>> GetAllUser()
        {
            return await _context.User.ToListAsync();
        }

        public async Task<User> GetUserById(Guid id)
        {
            return await _context.User.FindAsync(id);
        }

        public async Task<User> PostUser(User user)
        {
                await _context.User.AddAsync(user);
                await _context.SaveChangesAsync();
                return user;
        }

        public async Task<User> UpdateUserById(User user)
        {
            _context.User.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }
    }
}
