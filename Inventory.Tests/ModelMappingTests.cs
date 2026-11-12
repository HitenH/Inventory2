using Inventory.Domain.Entities;
using Inventory.Models;

namespace Inventory.Tests;

/// <summary>Copies between database records and screen models (batch 12, replaced AutoMapper).</summary>
public class ModelMappingTests
{
    [Fact]
    public void Customer_copy_keeps_every_field_and_uses_a_new_phone_list()
    {
        var phone = new Mobile();
        var entity = new CustomerEntity
        {
            Id = Guid.NewGuid(), CustomerId = "C1", Name = "Ann", ContactPerson = "Bob",
            Address = "Street", Area = "North", Remarks = "VIP", Mobiles = new() { phone },
        };

        var model = entity.ToModel();
        Assert.Equal((entity.Id, "C1", "Ann", "Bob", "Street", "North", "VIP"),
            (model.Id, model.CustomerId, model.Name, model.ContactPerson, model.Address, model.Area, model.Remarks));
        Assert.Same(phone, Assert.Single(model.Mobiles));
        Assert.NotSame(entity.Mobiles, model.Mobiles); // editing phones on screen must not change the loaded record's list

        var back = model.ToEntity();
        Assert.Equal((entity.Id, "C1", "Ann"), (back.Id, back.CustomerId, back.Name));
        Assert.Same(phone, Assert.Single(back.Mobiles!));
    }

    [Fact]
    public void Missing_lists_become_empty_lists()
    {
        var model = new SupplierEntity { Id = Guid.NewGuid(), Mobiles = null, Purchases = null }.ToModel();
        Assert.Empty(model.Mobiles);
        Assert.Empty(model.Purchases);
    }

    [Fact]
    public void Product_and_variant_copies_keep_category_image_and_reorder_level()
    {
        var category = new CategoryEntity { Id = Guid.NewGuid(), Name = "Bags" };
        var image = new Image();
        var variant = new VariantEntity { Id = Guid.NewGuid(), VariantId = "V1", Name = "Red", ReorderLevel = 3, Image = image };
        var product = new ProductEntity { Id = Guid.NewGuid(), ProductId = "P1", Name = "Tote", Rate = 9.5m, Description = "d", Category = category, Variants = new() { variant } };

        var pm = product.ToModel();
        Assert.Equal(("P1", "Tote", (decimal?)9.5m, "d"), (pm.ProductId, pm.Name, pm.Rate, pm.Description));
        Assert.Same(category, pm.Category);
        Assert.Same(variant, Assert.Single(pm.Variants!));

        var vm = variant.ToModel();
        Assert.Equal(("V1", "Red", (int?)3), (vm.VariantId, vm.Name, vm.ReorderLevel));
        Assert.Same(image, vm.Image);
        Assert.Null(vm.StockInHand);

        var ve = vm.ToEntity();
        Assert.Equal((variant.Id, "V1", (int?)3), (ve.Id, ve.VariantId, ve.ReorderLevel));
        Assert.Same(image, ve.Image);
    }
}
