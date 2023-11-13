using Inventory.Domain.Entities;

namespace Inventory.Models
{
    public class CustomerModel
    {
        public Guid Id { get; set; }
        public string CustomerId { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string Address { get; set; }
        public string Area { get; set; }
        public string Remarks { get; set; }
        public List<Mobile> Mobiles { get; set; } = new();
        public List<SalesOrderEntity>? SalesOrders { get; set; }
        public List<SalesEntity>? Sales { get; set; } = new();

    }
}
