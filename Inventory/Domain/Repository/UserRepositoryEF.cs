using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class UserRepositoryEF : IUserRepository
    {
        private readonly AppDbContext context;

        public UserRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(UserEntity user)
        {
           await context.Users.AddAsync(user);
           await context.SaveChangesAsync();
        }

        public async Task<UserEntity> GetByName(string name)
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Name == name, default);
        }

        public async Task<UserEntity> GetById(int id)
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Id == id, default);
        }

        public async Task<IEnumerable<UserEntity>> GetAll()
        {
            return await context.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync();
        }

        public async Task Update(UserEntity user)
        {
            var existing = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing is null)
            {
                return;
            }

            existing.Name = user.Name;
            existing.Password = user.Password;
            existing.Role = user.Role;
            await context.SaveChangesAsync();
        }

        public async Task Delete(UserEntity user)
        {
            var existing = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing is null)
            {
                return;
            }

            context.Users.Remove(existing);
            await context.SaveChangesAsync();
        }
    }
}
