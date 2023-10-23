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

        Task<IEnumerable<UserEntity>> IUserRepository.GetAll()
        {
            throw new NotImplementedException();
        }

        Task IUserRepository.Update(UserEntity user)
        {
            throw new NotImplementedException();
        }
        Task IUserRepository.Delete(UserEntity user)
        {
            throw new NotImplementedException();
        }
    }
}
