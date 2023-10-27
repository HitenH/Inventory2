using Inventory.Domain.Entities;
namespace Inventory.Service
{
    public interface INumberService
    {
        List<Number> GetNumbers(List<Number> list);
    }
}
