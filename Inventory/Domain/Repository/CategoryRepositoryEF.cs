using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class CategoryRepositoryEF : ICategoryRepository
    {
        private readonly AppDbContext context;

        public CategoryRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(CategoryEntity category)
        {
            await context.Categories.AddAsync(category);
            await context.SaveChangesAsync();
        }

        public async Task Delete(CategoryEntity category)
        {
            context.Categories.Remove(category);
            await context.SaveChangesAsync();
        }

        public async Task<List<CategoryEntity>> GetAll()
        {
            return await context.Categories.ToListAsync();
        }

        public async Task<CategoryEntity> GetById(Guid id)
        {
            return await context.Categories.FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(CategoryEntity category)
        {
            context.Categories.Update(category);
            await context.SaveChangesAsync();
        }
    }
}
