using System.Text;

namespace Inventory.Service.Import;

public static class ImportEndpoints
{
    /// <summary>
    /// GET /import-sample/customers | suppliers | products: the sample CSV as a download.
    /// The file holds only headings and made-up example rows, so it needs no login (like /photos).
    /// </summary>
    public static void MapImportSamples(this IEndpointRouteBuilder app)
    {
        app.MapGet("/import-sample/{kind}", (string kind) =>
        {
            if (!Enum.TryParse<ImportKind>(kind, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                return Results.NotFound();

            // UTF-8 with a byte-order mark so Excel opens it with the right characters.
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ImportTemplates.SampleCsv(parsed))).ToArray();
            return Results.File(bytes, "text/csv", ImportTemplates.SampleFileName(parsed));
        });
    }
}
