using Inventory.Domain.Entities;

namespace Inventory.Domain.Repository.Abstract
{
    public interface IImageRepository
    {
        Task Create(Image image);
        Task<Image> GetById(Guid id);
        Task<List<Image>> GetAll();
        Task Update(Image image);
        Task Delete(Image image);
    }
}
