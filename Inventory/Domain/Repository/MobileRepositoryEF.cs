using Inventory.Domain.Entities;
using Inventory.Domain.Repository.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Domain.Repository
{
    public class MobileRepositoryEF : IMobileRepository
    {
        private readonly AppDbContext context;

        public MobileRepositoryEF(AppDbContext context)
        {
            this.context = context;
        }
        public async Task Create(Mobile mobile)
        {
            await context.Mobiles.AddAsync(mobile);
            await context.SaveChangesAsync();
        }

        public async Task Delete(Mobile mobile)
        {
            context.Mobiles.Remove(mobile);
            await context.SaveChangesAsync();
        }

        public async Task DeleteRange(List<Mobile> mobiles)
        {
            context.Mobiles.RemoveRange(mobiles);
            await context.SaveChangesAsync();
        }

        public async Task<List<Mobile>> GetAll()
        {
             return await context.Mobiles.ToListAsync();
        }

        public async Task<Mobile> GetById(int id)
        {
            return await context.Mobiles.FirstOrDefaultAsync(c => c.Id == id, default);
        }

        public async Task Update(Mobile mobile)
        {
            context.Mobiles.Update(mobile);
            await context.SaveChangesAsync();
        }
    }
}
