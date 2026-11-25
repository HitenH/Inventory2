using QRCoder;
using System.Globalization;
using System.Text;

namespace Inventory.Service;

/// <summary>One line of the print list: a product + variant and how many identical labels.</summary>
public sealed record LabelItem(string ProductId, string ProductName, string VariantId, string VariantName, int Copies);

/// <summary>Where one label lands: sheet (0-based), row and column on that sheet.</summary>
public sealed record LabelSpot(int Sheet, int Row, int Column, LabelItem Item);

/// <summary>
/// A named label set-up: the sheet of pre-cut labels and what sits inside one label. Version 3.0, stage L (Hiten,
/// 25 Nov 2026): kept in the database as named labels (Domain/Entities/LabelLayoutEntity.cs, Service/LabelLayoutService.cs),
/// changed on the Label settings page; Hiten makes a new one for another sheet or printer and switches between them.
/// <see cref="A4Sheet65"/> holds the first one, "A4 65 Label", measured from Hiten's Word file and adjusted after two printer tests.
/// </summary>
public sealed record LabelSheetLayout(string Name, double PageWidthMm, double PageHeightMm, int Columns, int Rows,
    double LabelWidthMm, double LabelHeightMm, double TopMm, double LeftMm)
{
    public int Id { get; init; }
    public bool IsActive { get; init; }
    public double GapAcrossMm { get; init; }
    public double GapDownMm { get; init; }

    /// <summary>Everything inside a label moves down / left by this much (Hiten's printer tests: 2.2 and 0.5 mm).</summary>
    public double ContentDownMm { get; init; }
    public double ContentLeftMm { get; init; }

    public double QrMm { get; init; } = 13;
    public double NameFontPt { get; init; } = 9;
    public double VariantFontPt { get; init; } = 11;
    public double IdFontPt { get; init; } = 7.5;

    public const string FirstName = "A4 65 Label";

    public static readonly LabelSheetLayout A4Sheet65 = new(FirstName, 210, 297, 5, 13, 40, 21, 12, 4)
    {
        ContentDownMm = 2.2, ContentLeftMm = 0.5, QrMm = 13, NameFontPt = 9, VariantFontPt = 11, IdFontPt = 7.5,
    };

    public int PerSheet => Columns * Rows;

    public double LeftOf(int column) => LeftMm + column * (LabelWidthMm + GapAcrossMm);
    public double TopOf(int row) => TopMm + row * (LabelHeightMm + GapDownMm);

    /// <summary>
    /// Plain reasons why these numbers cannot be used, or none. Checked before saving, so a label set-up that does not fit
    /// its page can never be chosen for printing.
    /// </summary>
    public List<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name)) problems.Add("Give the label a name.");
        if (PageWidthMm <= 0 || PageHeightMm <= 0) problems.Add("The page width and height must be more than 0 mm.");
        if (Columns < 1 || Columns > 50 || Rows < 1 || Rows > 100) problems.Add("Labels across must be 1 to 50, labels down 1 to 100.");
        if (LabelWidthMm <= 0 || LabelHeightMm <= 0) problems.Add("The label width and height must be more than 0 mm.");
        if (GapAcrossMm < 0 || GapDownMm < 0 || TopMm < 0 || LeftMm < 0) problems.Add("Gaps and margins cannot be below 0 mm.");
        if (QrMm < 5) problems.Add("The QR code must be at least 5 mm, or scanners cannot read it.");
        if (NameFontPt < 4 || VariantFontPt < 4 || IdFontPt < 4 || NameFontPt > 30 || VariantFontPt > 30 || IdFontPt > 30) problems.Add("Text sizes must be 4 to 30 pt.");
        if (problems.Count > 0)
        {
            return problems;
        }

        var right = LeftOf(Columns - 1) + LabelWidthMm;
        var bottom = TopOf(Rows - 1) + LabelHeightMm;
        if (right > PageWidthMm + 0.001) problems.Add($"The labels reach {right:0.#} mm across, but the page is {PageWidthMm:0.#} mm wide.");
        if (bottom > PageHeightMm + 0.001) problems.Add($"The labels reach {bottom:0.#} mm down, but the page is {PageHeightMm:0.#} mm high.");
        if (QrMm > LabelHeightMm || QrMm > LabelWidthMm) problems.Add($"The QR code ({QrMm:0.#} mm) is bigger than the label.");
        return problems;
    }
}

/// <summary>The rule book for labels: what goes in the QR code, how many fit, where each lands, and the QR picture itself.</summary>
public static class Labels
{
    /// <summary>At most this many copies of one label in one go (10 sheets).</summary>
    public const int MaxCopies = 650;

    /// <summary>At most this many labels in one print (20 sheets).</summary>
    public const int MaxLabels = 1300;

    /// <summary>
    /// What the QR code holds: product ID, Tab, variant ID (Hiten, 25 Nov 2026). The scanner types it into the Sale screen:
    /// product box, Tab to the variant box. Must stay exactly this.
    /// </summary>
    public static string QrText(string productId, string variantId) => $"{productId.Trim()}\t{variantId.Trim()}";

    /// <summary>A plain reason why a line cannot be added, or null when it can.</summary>
    public static string? Check(LabelItem item, int labelsAlreadyListed)
    {
        if (string.IsNullOrWhiteSpace(item.ProductId) || string.IsNullOrWhiteSpace(item.VariantId))
        {
            return "Choose a product and a variant first.";
        }

        if (item.ProductId.Contains('\t') || item.VariantId.Contains('\t'))
        {
            return "This product or variant ID contains a Tab, which would confuse the scanner.";
        }

        if (item.Copies < 1 || item.Copies > MaxCopies)
        {
            return $"Copies must be a whole number from 1 to {MaxCopies}.";
        }

        if (labelsAlreadyListed + item.Copies > MaxLabels)
        {
            return $"At most {MaxLabels} labels can be printed in one go.";
        }

        return null;
    }

    /// <summary>
    /// Every label in order (Hiten, 25 Nov 2026): a sheet fills <b>from the bottom-right corner backwards</b> – the last row
    /// right to left, then the row above – so the unused labels stay together at the top. <paramref name="usedOnFirstSheet"/>
    /// labels are already gone from the first sheet (from earlier prints), so printing starts just above them; later sheets start full.
    /// </summary>
    public static List<LabelSpot> Arrange(IEnumerable<LabelItem> items, LabelSheetLayout layout, int usedOnFirstSheet = 0)
    {
        var spots = new List<LabelSpot>();
        var n = Math.Clamp(usedOnFirstSheet, 0, layout.PerSheet - 1);
        foreach (var item in items)
        {
            for (var copy = 0; copy < item.Copies; copy++, n++)
            {
                var (row, column) = SpotOf(n % layout.PerSheet, layout);
                spots.Add(new LabelSpot(n / layout.PerSheet, row, column, item));
            }
        }

        return spots;
    }

    /// <summary>Row and column of the n-th label of a sheet, counting backwards from the bottom-right corner.</summary>
    public static (int Row, int Column) SpotOf(int fromBottom, LabelSheetLayout layout)
    {
        var index = layout.PerSheet - 1 - fromBottom;
        return (index / layout.Columns, index % layout.Columns);
    }

    /// <summary>The labels already used on the first sheet (shown greyed in the preview).</summary>
    public static List<(int Row, int Column)> UsedSpots(int usedOnFirstSheet, LabelSheetLayout layout) =>
        Enumerable.Range(0, Math.Clamp(usedOnFirstSheet, 0, layout.PerSheet - 1)).Select(n => SpotOf(n, layout)).ToList();

    /// <summary>
    /// Where the next print starts after a print of <paramref name="printed"/> labels that began after <paramref name="used"/>
    /// (Hiten, 25 Nov 2026): 10 then 20 → 30 used; 40 more → the first sheet is finished and 5 of the next are used.
    /// A sheet used up exactly starts the next print on a fresh sheet (0).
    /// </summary>
    public static int UsedAfterPrint(int used, int printed, LabelSheetLayout layout) =>
        (Math.Clamp(used, 0, layout.PerSheet - 1) + Math.Max(printed, 0)) % layout.PerSheet;

    /// <summary>A plain reason why the "already used" number cannot be taken, or null.</summary>
    public static string? CheckUsed(int used, LabelSheetLayout layout) =>
        used < 0 || used >= layout.PerSheet ? $"Already used must be a whole number from 0 to {layout.PerSheet - 1}." : null;

    public static int SheetsFor(int labels, LabelSheetLayout layout, int usedOnFirstSheet = 0) =>
        labels == 0 ? 0 : (labels + Math.Clamp(usedOnFirstSheet, 0, layout.PerSheet - 1) + layout.PerSheet - 1) / layout.PerSheet;

    /// <summary>
    /// The QR code as a small SVG picture (sharp at any size, made on this PC, no internet). Medium error correction,
    /// plain byte text (no UTF-8 marker), with the standard white border around it.
    /// </summary>
    public static string QrSvg(string text)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix;
        var size = matrix.Count;
        var path = new StringBuilder();
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (matrix[y][x])
                {
                    path.Append(CultureInfo.InvariantCulture, $"M{x} {y}h1v1h-1z");
                }
            }
        }

        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" shape-rendering=\"crispEdges\" class=\"label-qr-svg\">"
             + $"<rect width=\"{size}\" height=\"{size}\" fill=\"#fff\"/><path d=\"{path}\" fill=\"#000\"/></svg>";
    }

    /// <summary>The sizes inside one label for the sheet's style (site.css .label-* reads them), from the chosen label set-up.</summary>
    public static string InsideStyle(LabelSheetLayout l) =>
        $"--label-down:{Mm(l.ContentDownMm)};--label-left:{Mm(l.ContentLeftMm)};--lw:{Mm(l.LabelWidthMm)};--qr:{Mm(l.QrMm)};" +
        $"--name-pt:{Pt(l.NameFontPt)};--variant-pt:{Pt(l.VariantFontPt)};--id-pt:{Pt(l.IdFontPt)}";

    public static string Pt(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "pt";

    /// <summary>"12.5" – a millimetre value for an inline style, always with a dot.</summary>
    public static string Mm(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "mm";
}
