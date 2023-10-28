using System.ComponentModel.DataAnnotations;

namespace Inventory.Domain.Entities
{
    public class Image
    {
        [Key]
        public int Id { get; set; }
        public string ImageTitle { get; set; }
        public byte[] ImageData { get; set; }
    }
}
