using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service
{
    public interface IMobileService
    {
        List<Mobile> GetMobiles(List<Mobile> list);
    }
}
