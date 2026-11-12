using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

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

    // DeleteEmptyNumbers (a sweep over every phone row in the database) was removed in batch 6:
    // Customer/Supplier repositories now add, update and delete the phone numbers of the one record being saved.
}
