using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Inventory.Pages;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class SalesSummaryRepositoryEF : ISalesSummaryRepository
    {
        private readonly AppDbContext context;

        public SalesSummaryRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task<Guid> Create(SalesSummaryEntity model)
        {
            await context.SalesSummaries.AddAsync(model);
            await context.SaveChangesAsync();
            return model.Id;
        }

        public async Task Delete(SalesSummaryEntity model)
        {
            context.SalesSummaries.Remove(model);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<SalesSummaryEntity> models)
        {
            context.SalesSummaries.RemoveRange(models);
            await context.SaveChangesAsync();
        }

        public async Task<List<SalesSummaryEntity>> GetAll()
        {
            return await context.SalesSummaries.ToListAsync();
        }

        public async Task<SalesSummaryEntity> GetById(Guid id)
        {
            return await context.SalesSummaries.Include(s => s.Customer).Include(s => s.SalesSummaryVariants).FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(SalesSummaryEntity model)
        {
            context.SalesSummaries.Update(model);
            await context.SaveChangesAsync();
        }
    }
}
