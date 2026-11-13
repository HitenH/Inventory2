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
        TotalSales = e.Sales?.Sum(s => s.TotalAmountProduct) ?? 0m,
    };

    /// <summary>Batch 14: lists get each customer's sales total from one database query instead of loading every sale.</summary>
    public static CustomerModel ToModel(this CustomerEntity e, IReadOnlyDictionary<Guid, decimal> salesTotals)
    {
        var model = e.ToModel();
        model.TotalSales = salesTotals.GetValueOrDefault(e.Id, 0m);
        return model;
    }

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
    };

    // ---- Product ----
    // The category itself is chosen on the Product page and set by that page when saving.
    public static ProductModel ToModel(this ProductEntity e) => new()
    {
        Id = e.Id,
        ProductId = e.ProductId,
        Name = e.Name,
        Rate = e.Rate,
        Description = e.Description,
        Variants = e.Variants?.Select(v => v.ToModel()).ToList() ?? new List<VariantModel>(),
    };

    public static ProductEntity ToEntity(this ProductModel m) => new()
    {
        Id = m.Id,
        ProductId = m.ProductId,
        Name = m.Name,
        Rate = m.Rate,
        Description = m.Description,
        Variants = m.Variants.Select(v => v.ToEntity()).ToList(),
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
