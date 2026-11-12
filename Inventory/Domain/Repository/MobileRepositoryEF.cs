using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    // Batch 6: each method uses its own short-lived context from the factory.
    public class MobileRepositoryEF : IMobileRepository
    {
        private readonly IDbContextFactory<AppDbContext> factory;

        public MobileRepositoryEF(IDbContextFactory<AppDbContext> factory)
        {
            this.factory = factory;
        }

        public async Task Create(Mobile item)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Mobiles.AddAsync(item);
            await context.SaveChangesAsync();
        }

        public async Task Delete(Mobile item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Mobiles.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Mobiles.Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<Mobile> items)
        {
            await using var context = await factory.CreateDbContextAsync();
            var ids = items.Select(i => i.Id).ToList();
            var existing = await context.Mobiles.Where(m => ids.Contains(m.Id)).ToListAsync();
            context.Mobiles.RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<Mobile>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Mobiles.AsNoTracking().ToListAsync();
        }

        public async Task<Mobile> GetById(int id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Mobiles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        }

        /// <summary>Loads the row, copies the changed values onto it, saves.</summary>
        public async Task Update(Mobile item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Mobiles.FindAsync(item.Id);
            if (existing is null)
                return;

            context.Entry(existing).CurrentValues.SetValues(item);
            await context.SaveChangesAsync();
        }
    }
}
