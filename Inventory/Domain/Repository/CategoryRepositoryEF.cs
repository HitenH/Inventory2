using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class CategoryRepositoryEF : ICategoryRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public CategoryRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(CategoryEntity item)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Categories.AddAsync(item);
            await context.SaveChangesAsync();
        }

        public async Task Delete(CategoryEntity item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Categories.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Categories.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<CategoryEntity>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Categories.AsNoTracking().ToListAsync();
        }

        public async Task<CategoryEntity> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>Loads the row, copies the changed values onto it, saves.</summary>
        public async Task Update(CategoryEntity item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Categories.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Entry(existing).CurrentValues.SetValues(item);
            await context.SaveChangesAsync();
        }
    }
}
