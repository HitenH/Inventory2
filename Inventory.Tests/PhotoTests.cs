using Inventory.Domain.Entities;
using Inventory.Service;

namespace Inventory.Tests;

/// <summary>Batch 14: photos are fetched from /photos/{id} instead of being packed into the page.</summary>
public class PhotoTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9 };

    [Fact]
    public void A_saved_photo_gets_an_address_that_changes_when_the_picture_changes()
    {
        var id = Guid.NewGuid();
        var first = Photos.UrlFor(new Image { Id = id, ImageData = Jpeg });
        var replaced = Photos.UrlFor(new Image { Id = id, ImageData = Png });

        Assert.StartsWith($"/photos/{id}?v=", first);
        Assert.NotEqual(first, replaced);
        Assert.Equal(first, Photos.UrlFor(new Image { Id = id, ImageData = (byte[])Jpeg.Clone() }));
    }

    [Fact]
    public void A_photo_not_saved_yet_is_still_shown_inside_the_page()
    {
        Assert.Equal("data:image/jpeg;base64," + Convert.ToBase64String(Jpeg), Photos.UrlFor(new Image { ImageData = Jpeg }));
        Assert.Equal("", Photos.UrlFor(new Image()));
    }

    [Fact]
    public void Picture_type_is_recognised()
    {
        Assert.Equal("image/jpeg", Photos.ContentTypeOf(Jpeg));
        Assert.Equal("image/png", Photos.ContentTypeOf(Png));
        Assert.Equal("image/gif", Photos.ContentTypeOf("GIF89a"u8.ToArray()));
        Assert.Equal("image/jpeg", Photos.ContentTypeOf(new byte[] { 1, 2 }));   // unknown: JPEG, as before
    }

    [Fact]
    public async Task Only_the_requested_photo_is_read_from_the_database()
    {
        using var db = new TestDatabase();
        var seed = await db.SeedAsync();
        var photoId = Guid.NewGuid();
        await using (var c = db.Open())
        {
            c.Images.Add(new Image { Id = photoId, ImageTitle = "red.jpg", ImageData = Jpeg, VariantEntityId = seed.Variant1Id });
            await c.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(Jpeg, await Photos.LoadAsync(db.Factory, photoId, TestContext.Current.CancellationToken));
        Assert.Null(await Photos.LoadAsync(db.Factory, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }
}
