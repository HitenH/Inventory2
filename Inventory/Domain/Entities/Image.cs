using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class Image
    {
        [Key]
        public Guid Id { get; set; }
        public string? ImageTitle { get; set; }
        public byte[]? ImageData { get; set; }
        public Guid? VariantEntityId { get; set; }
        public VariantEntity Variant { get; set; }
    }
}
