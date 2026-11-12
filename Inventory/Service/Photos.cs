using System.Security.Cryptography;
using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Service;

/// <summary>
/// Batch 14 (P3): variant photos stay in the database (so backups still contain them), but the browser
/// fetches each one as a normal image from /photos/{id} instead of receiving it packed into the page as text.
/// </summary>
public static class Photos
{
    public const string RoutePrefix = "/photos/";

    /// <summary>
    /// Where the browser gets a photo. A saved photo has its own address; a photo that is not saved yet
    /// (no ID) is still shown the old way, packed into the page.
    /// </summary>
    public static string UrlFor(Image image)
    {
        if (image.Id != Guid.Empty)
        {
            // The fingerprint changes when the picture changes, so the screen never keeps showing an old photo.
            return image.ImageData is { Length: > 0 } saved
                ? $"{RoutePrefix}{image.Id}?v={ETagOf(saved).Trim('"')[..8]}"
                : RoutePrefix + image.Id;
        }
        return image.ImageData is { Length: > 0 } data
            ? $"data:{ContentTypeOf(data)};base64,{Convert.ToBase64String(data)}"
            : "";
    }

    /// <summary>Picture type from the first bytes of the file. Uploads are JPEG; anything unknown is sent as JPEG, as before.</summary>
    public static string ContentTypeOf(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";
        if (data.Length >= 6 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F')
            return "image/gif";
        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
            && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P')
            return "image/webp";
        return "image/jpeg";
    }

    /// <summary>A fingerprint of the photo: the browser re-uses its copy until the photo is replaced.</summary>
    public static string ETagOf(byte[] data) => "\"" + Convert.ToHexString(SHA256.HashData(data))[..16] + "\"";

    /// <summary>Reads only the picture bytes of one photo (no product, no variant).</summary>
    public static async Task<byte[]?> LoadAsync(IDbContextFactory<AppDbContext> factory, Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.Images
            .AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => i.ImageData)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>GET /photos/{id}: the photo, or 404 when there is none.</summary>
    public static void MapPhotos(this WebApplication app)
    {
        app.MapGet(RoutePrefix + "{id:guid}", async (Guid id, HttpContext http, IDbContextFactory<AppDbContext> factory) =>
        {
            var data = await Photos.LoadAsync(factory, id, http.RequestAborted);
            if (data is not { Length: > 0 })
            {
                return Results.NotFound();
            }

            var etag = ETagOf(data);
            http.Response.Headers.CacheControl = "no-cache";   // the browser checks each time; unchanged photos are not re-sent
            http.Response.Headers.ETag = etag;
            if (http.Request.Headers.IfNoneMatch == etag)
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
            return Results.File(data, ContentTypeOf(data));
        });
    }
}
