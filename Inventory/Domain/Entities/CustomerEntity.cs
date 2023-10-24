using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class CustomerEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string? CustomerId { get; set; }
        public string? Name { get; set; }
        public string? ContactPerson { get; set; }
        public string? Address { get; set; }
        public string? Area { get; set; }
        public string? Remarks { get; set; }
        public List<Number>? Numbers { get; set; }
    }

    public class Number
    {
        public int Id { get; set; }
        public string? Phone { get; set; }
    }
}
