using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class CategoryEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string? CategoryId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<ProductEntity>? Products { get; set; }
    }
}
