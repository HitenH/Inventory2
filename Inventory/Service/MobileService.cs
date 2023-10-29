using Inventory.Domain.Entities;

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
    }
}
