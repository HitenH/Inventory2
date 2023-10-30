using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service
{
    public class MobileService: IMobileService
    {
        public List<Mobile> GetMobiles(List<Mobile> list)
        {
            var mobiles= new List<Mobile>();
            foreach (var item in list)
            {
                if (item.Phone != "")
                    mobiles.Add(item);
            }
            return mobiles;
        }

        public async Task DeleteEmptyNumbers(AppDbContext context)
        {
            var mobiles = await context.Mobiles.Where(m => m.Phone == "").ToListAsync();
            if (mobiles.Count > 0)
            {
                context.Mobiles.RemoveRange(mobiles);
                await context.SaveChangesAsync();
            }
        }
    }
}
