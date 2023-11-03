namespace Inventory.Domain.Entities
{
    public class Mobile
    {
        public int Id { get; set; }
        public string? Phone { get; set; }
        public Guid? CustomerEntityId { get; set; }
        public CustomerEntity? Customer { get; set; }
        public Guid? SupplierEntityId { get; set; }
        public SupplierEntity? Supplier { get; set; }
    }
}
