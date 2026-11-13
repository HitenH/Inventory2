using System.Reflection;
using Inventory.Models;
using Inventory.Pages;

namespace Inventory.Tests;

/// <summary>Cancelling an edit must leave the table row alone: forms edit a copy; and the Sale summary rounds to 2 decimals.</summary>
public class EditCopyTests
{
    /// <summary>Gives every public settable property a non-default value (so a property missed by a copy is noticed).</summary>
    private static void FillAll(object o)
    {
        foreach (var p in o.GetType().GetProperties().Where(p => p.CanWrite))
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            object? v;
            if (t == typeof(Guid)) v = Guid.NewGuid();
            else if (t == typeof(int)) v = 7;
            else if (t == typeof(decimal)) v = 12.34m;
            else if (t == typeof(bool)) v = true;
            else if (t == typeof(string)) v = "x-" + p.Name;
            else if (t == typeof(DateOnly)) v = new DateOnly(2031, 5, 6);
            else if (t.IsEnum) v = Enum.GetValues(t).GetValue(Enum.GetValues(t).Length - 1);
            else if (t == typeof(ProductModel)) v = new ProductModel { Name = "p" };
            else throw new InvalidOperationException($"FillAll does not know {t} ({p.Name})");
            p.SetValue(o, v);
        }
    }

    private static void AssertSameValues(object a, object b)
    {
        foreach (var p in a.GetType().GetProperties().Where(p => p.CanWrite))
            Assert.True(Equals(p.GetValue(a), p.GetValue(b)), $"{p.Name} was not copied");
    }

    private static void ChangeAll(object o)
    {
        foreach (var p in o.GetType().GetProperties().Where(p => p.CanWrite))
        {
            var t = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
            if (t == typeof(Guid)) p.SetValue(o, Guid.NewGuid());
            else if (t == typeof(int)) p.SetValue(o, 99);
            else if (t == typeof(decimal)) p.SetValue(o, 99.9m);
            else if (t == typeof(bool)) p.SetValue(o, false);
            else if (t == typeof(string)) p.SetValue(o, "changed");
            else if (t == typeof(DateOnly)) p.SetValue(o, new DateOnly(2040, 1, 1));
            else if (t.IsEnum) p.SetValue(o, Enum.GetValues(t).GetValue(0));
            else if (t == typeof(ProductModel)) p.SetValue(o, new ProductModel { Name = "other" });
        }
    }

    [Fact]
    public void LineEditorModel_Clone_copies_every_property_and_is_independent()
    {
        var original = new LineEditorModel();
        FillAll(original);
        var copy = original.Clone();
        Assert.NotSame(original, copy);
        AssertSameValues(original, copy);

        ChangeAll(copy);
        Assert.Equal("x-ProductId", original.ProductId);
        Assert.Equal(7, original.Quantity);
    }

    [Fact]
    public void SalesVariantModel_Clone_copies_every_property_and_is_independent()
    {
        var original = new SalesVariantModel();
        FillAll(original);
        var product = original.Product;
        var copy = original.Clone();
        Assert.NotSame(original, copy);
        AssertSameValues(original, copy);

        ChangeAll(copy);
        Assert.Equal("x-ProductId", original.ProductId);
        Assert.Equal("x-VariantId", original.VariantId);
        Assert.Equal(7, original.Quantity);
        Assert.Same(product, original.Product);
        Assert.True(original.IsCreated);
    }

    [Fact]
    public void PurchaseOrderModel_Clone_and_CopyFrom_cover_every_property()
    {
        var original = new PurchaseOrderModel();
        FillAll(original);
        var backup = original.Clone();
        Assert.NotSame(original, backup);
        AssertSameValues(original, backup);

        ChangeAll(original);
        original.CopyFrom(backup);
        AssertSameValues(backup, original);

        // CopyFrom copies from a fully filled model, so a forgotten property would fail here
        var target = new PurchaseOrderModel();
        target.CopyFrom(backup);
        AssertSameValues(backup, target);
    }

    [Theory]
    [InlineData(333.33, 1, 15, 333.33, 283.33)]
    [InlineData(33.33, 1, 7, 33.33, 31.00)]
    [InlineData(10, 2, 0, 20, 20)]
    [InlineData(0, 5, 10, 0, 0)]
    public void Summary_line_amounts_are_rounded_to_two_decimals(double rate, int qty, int discount, double amount, double after)
    {
        var (a, d) = SalesSummary.ComputeLineAmounts((decimal)rate, qty, discount);
        Assert.Equal((decimal)amount, a);
        Assert.Equal((decimal)after, d);
        Assert.Equal(Math.Round(a, 2), a);
        Assert.Equal(Math.Round(d, 2), d);
    }
}
