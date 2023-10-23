using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IUserRepository
    {
        Task Create(UserEntity user);
        Task<IEnumerable<UserEntity>> GetAll();
        Task<UserEntity> GetById();
        Task Update(UserEntity user);
        Task Delete(UserEntity user);
    }
}
