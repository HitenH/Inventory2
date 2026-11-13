using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    /// <summary>
    /// The plain add / read / change / delete used by records with no special rules
    /// (categories, photos, purchase orders). Batch 12: one copy instead of a copy per record type.
    /// Each method uses its own short-lived context (batch 6).
    /// </summary>
    public abstract class SimpleRepositoryEF<T> where T : class
    {
        private readonly IDbContextFactory<AppDbContext> factory;
        private readonly Func<T, Guid> idOf;

        protected SimpleRepositoryEF(IDbContextFactory<AppDbContext> factory, Func<T, Guid> idOf)
        {
            this.factory = factory;
            this.idOf = idOf;
        }

        public async Task Create(T item)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Set<T>().AddAsync(item);
            await context.SaveChangesAsync();
        }

        /// <summary>Adds several rows in one save: all are saved or none (bulk entry, Nov 2026).</summary>
        public async Task CreateRange(List<T> items)
        {
            if (items.Count == 0)
                return;
            await using var context = await factory.CreateDbContextAsync();
            await context.Set<T>().AddRangeAsync(items);
            await context.SaveChangesAsync();
        }

        public async Task Delete(T item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Set<T>().FindAsync(idOf(item));
            if (existing is null)
                return;

            context.Set<T>().Remove(existing);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<T> items)
        {
            await using var context = await factory.CreateDbContextAsync();
            var ids = items.Select(idOf).ToList();
            var existing = await context.Set<T>().Where(e => ids.Contains(EF.Property<Guid>(e, "Id"))).ToListAsync();
            context.Set<T>().RemoveRange(existing);
            await context.SaveChangesAsync();
        }

        public async Task<List<T>> GetAll()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<T> GetById(Guid id)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);
        }

        /// <summary>Loads the row, copies the changed values onto it, saves.</summary>
        public async Task Update(T item)
        {
            await using var context = await factory.CreateDbContextAsync();
            var existing = await context.Set<T>().FindAsync(idOf(item));
            if (existing is null)
                return;

            context.Entry(existing).CurrentValues.SetValues(item);
            await context.SaveChangesAsync();
        }
    }
}
