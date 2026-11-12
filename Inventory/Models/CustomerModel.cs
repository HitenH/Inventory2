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
        /// <summary>Sum of this customer's sale totals (filled only when the sales were loaded, e.g. the Customers list).</summary>
        public decimal TotalSales { get; set; }
    }
}
