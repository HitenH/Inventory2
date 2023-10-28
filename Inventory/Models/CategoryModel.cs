using System.ComponentModel.DataAnnotations;

namespace Inventory.Models
{
    public class CategoryModel
    {
        public Guid Id { get; set; }
        [Required]
        public string? CategoryId { get; set; }
        [Required]
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}
