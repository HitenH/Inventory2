using Inventory.Domain.Entities;

namespace Inventory.Service
{
    public class NumberService: INumberService
    {
        public List<Number> GetNumbers(List<Number> list)
        {
            var numbers = new List<Number>();
            foreach (var item in list)
            {
                if (item.Phone != "")
                    numbers.Add(item);
            }
            return numbers;
        }
    }
}
