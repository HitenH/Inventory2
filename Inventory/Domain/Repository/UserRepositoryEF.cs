using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;

namespace Inventory.Domain.Repository
{
    public class UserRepositoryEF : IUserRepository
    {
        public async Task Create(UserEntity user)
        {
           
        }

        Task<UserEntity> IUserRepository.GetById()
        {
            throw new NotImplementedException();
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
