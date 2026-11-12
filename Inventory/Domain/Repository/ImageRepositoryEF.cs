using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class ImageRepositoryEF : IImageRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public ImageRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(Image item)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Images.AddAsync(item);
            await context.SaveChangesAsync();
        }

        public async Task Delete(Image item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Images.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Images.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<Image>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Images.AsNoTracking().ToListAsync();
        }

        public async Task<Image> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Images.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>Loads the row, copies the changed values onto it, saves.</summary>
        public async Task Update(Image item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Images.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Entry(existing).CurrentValues.SetValues(item);
            await context.SaveChangesAsync();
        }
    }
}
