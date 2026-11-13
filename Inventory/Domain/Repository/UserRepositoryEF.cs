using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class UserRepositoryEF : IUserRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public UserRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }
        public async Task Create(UserEntity user)
        {
            await using var context = await factory.CreateDbContextAsync();
           await context.Users.AddAsync(user);
           await context.SaveChangesAsync();
        }

        public async Task<UserEntity> GetByName(string name)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Users.FirstOrDefaultAsync(u => u.Name == name, default);
        }

        public async Task<UserEntity> GetById(int id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Users.FirstOrDefaultAsync(u => u.Id == id, default);
        }

        public async Task<IEnumerable<UserEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Users.AsNoTracking().OrderBy(u => u.Name).ToListAsync();
        }

        public async Task Update(UserEntity user)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existing is null)
            {
                return;
            }

            existing.Name = user.Name;
            existing.Password = user.Password;
            await context.SaveChangesAsync();
        }

        public async Task Delete(UserEntity user)
        {
            await using var context = await factory.CreateDbContextAsync();
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
