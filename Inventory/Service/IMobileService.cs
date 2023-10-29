using Inventory.Domain.Entities;
namespace Inventory.Service
{
    public interface IMobileService
    {
        List<Mobile> GetMobiles(List<Mobile> list);
    }
}
