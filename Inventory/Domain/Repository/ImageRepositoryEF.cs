using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class ImageRepositoryEF : IImageRepository
    {
        private readonly AppDbContext context;

        public ImageRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(Image image)
        {
            await context.Images.AddAsync(image);
            await context.SaveChangesAsync();
        }

        public async Task Delete(Image image)
        {
            context.Images.Remove(image);
            await context.SaveChangesAsync();
        }

        public async Task<List<Image>> GetAll()
        {
            return await context.Images.ToListAsync();
        }

        public async Task<Image> GetById(Guid id)
        {
            return await context.Images.FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(Image image)
        {
            context.Images.Update(image);
            await context.SaveChangesAsync();
        }
    }
}
