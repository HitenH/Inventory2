using Inventory.Domain.Entities;

namespace Inventory.Models;

/// <summary>
/// Plain copies between database records (entities) and screen models (batch 12; replaces AutoMapper).
/// Kept identical to what AutoMapper did: same-named properties are copied, lists are copied into a
/// NEW list (the items themselves are shared), a missing list becomes an empty list, and properties
/// that exist on only one side are left at their default.
/// </summary>
public static class ModelMapping
{
    private static List<T> CopyList<T>(List<T>? source) => source is null ? new List<T>() : new List<T>(source);

    // ---- Customer ----
    public static CustomerModel ToModel(this CustomerEntity e) => new()
    {
        Id = e.Id,
        CustomerId = e.CustomerId,
        Name = e.Name,
        ContactPerson = e.ContactPerson,
        Address = e.Address,
        Area = e.Area,
        Remarks = e.Remarks,
        Mobiles = CopyList(e.Mobiles),
        SalesOrders = CopyList(e.SalesOrders),
        Sales = CopyList(e.Sales),
    };

    public static CustomerEntity ToEntity(this CustomerModel m) => new()
    {
        Id = m.Id,
        CustomerId = m.CustomerId,
        Name = m.Name,
        ContactPerson = m.ContactPerson,
        Address = m.Address,
        Area = m.Area,
        Remarks = m.Remarks,
        Mobiles = CopyList(m.Mobiles),
        SalesOrders = CopyList(m.SalesOrders),
        Sales = CopyList(m.Sales),
    };

    // ---- Supplier ----
    public static SupplierModel ToModel(this SupplierEntity e) => new()
    {
        Id = e.Id,
        SupplierId = e.SupplierId,
        Name = e.Name,
        ContactPerson = e.ContactPerson,
        Address = e.Address,
        Area = e.Area,
        Remarks = e.Remarks,
        Mobiles = CopyList(e.Mobiles),
        Purchases = CopyList(e.Purchases),
    };

    public static SupplierEntity ToEntity(this SupplierModel m) => new()
    {
        Id = m.Id,
        SupplierId = m.SupplierId,
        Name = m.Name,
        ContactPerson = m.ContactPerson,
        Address = m.Address,
        Area = m.Area,
        Remarks = m.Remarks,
        Mobiles = CopyList(m.Mobiles),
        Purchases = CopyList(m.Purchases),
    };

    // ---- Product ----
    // CategoryEntityId is only used by the import and was never filled from a record.
    public static ProductModel ToModel(this ProductEntity e) => new()
    {
        Id = e.Id,
        ProductId = e.ProductId,
        Name = e.Name,
        Rate = e.Rate,
        Description = e.Description,
        CategoryEntityId = null,
        Category = e.Category,
        Variants = CopyList(e.Variants),
    };

    public static ProductEntity ToEntity(this ProductModel m) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        Name = m.Name,
        Rate = m.Rate,
        Description = m.Description,
        Category = m.Category,
        Variants = CopyList(m.Variants),
    };

    // ---- Category ----
    public static CategoryModel ToModel(this CategoryEntity e) => new()
    {
        Id = e.Id,
        CategoryId = e.CategoryId,
        Name = e.Name,
        Description = e.Description,
    };

    public static CategoryEntity ToEntity(this CategoryModel m) => new()
    {
        Id = m.Id,
        CategoryId = m.CategoryId,
        Name = m.Name,
        Description = m.Description,
    };

    // ---- Variant ----
    // StockInHand is worked out separately; Product/ProductEntityId are set by the product save.
    public static VariantModel ToModel(this VariantEntity e) => new()
    {
        Id = e.Id,
        VariantId = e.VariantId,
        Name = e.Name,
        ReorderLevel = e.ReorderLevel,
        Image = e.Image,
    };

    public static VariantEntity ToEntity(this VariantModel m) => new()
    {
        Id = m.Id,
        VariantId = m.VariantId,
        Name = m.Name,
        ReorderLevel = m.ReorderLevel,
        Image = m.Image,
    };
}
