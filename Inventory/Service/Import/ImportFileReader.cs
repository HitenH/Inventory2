using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Inventory.Service.Import;

/// <summary>One data row of the file as typed: its Excel row number (heading row = 1) and its cells, trimmed.</summary>
public sealed record RawRow(int RowNumber, IReadOnlyList<string> Cells);

/// <summary>The file as a plain table of text: the heading cells and the data rows. Every row has as many cells as the headings.</summary>
public sealed record RawTable(IReadOnlyList<string> Headings, IReadOnlyList<RawRow> Rows);

/// <summary>The file cannot be read at all. The message is written for the operator.</summary>
public sealed class ImportFileException(string message) : Exception(message);

/// <summary>
/// Turns a .csv or .xlsx file into a <see cref="RawTable"/>. No database and no business rules here:
/// only "can we read the file, and what text is in each cell".
/// </summary>
public static class ImportFileReader
{
    public const int MaxRows = 5000;

    public static async Task<RawTable> ReadAsync(Stream stream, string fileName)
    {
        var extension = CheckExtension(fileName);
        // A browser upload stream allows asynchronous reads only, so copy it to memory first.
        var memory = new MemoryStream();
        await stream.CopyToAsync(memory);
        memory.Position = 0;
        return Parse(memory, extension);
    }

    public static RawTable Read(Stream stream, string fileName)
    {
        var extension = CheckExtension(fileName);
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return Parse(memory, extension);
    }

    private static string CheckExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        if (extension is ".csv" or ".xlsx")
            return extension;
        throw new ImportFileException("Only .csv and .xlsx files can be imported. Save the file as CSV or Excel Workbook (.xlsx) and try again.");
    }

    private static RawTable Parse(MemoryStream memory, string extension) =>
        extension == ".csv" ? ShapeTable(ParseCsv(memory)) : ShapeTable(ParseXlsx(memory));

    // ---- CSV ------------------------------------------------------------------------------------------------

    private static List<List<string>> ParseCsv(MemoryStream memory)
    {
        var text = DecodeText(memory.ToArray()).TrimStart('﻿');
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        void EndRecord()
        {
            row.Add(field.ToString());
            field.Clear();
            records.Add(row);
            row = new List<string>();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != '"')
                    field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');   // "" inside quotes is one quote character
                    i++;
                }
                else
                    inQuotes = false;
            }
            else if (c == '"' && field.Length == 0)
                inQuotes = true;
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                EndRecord();
            }
            else
                field.Append(c);
        }

        if (inQuotes)
            throw new ImportFileException("The file has a quote mark (\") that is never closed. Check the quotes around the text and try again.");
        if (field.Length > 0 || row.Count > 0)
            EndRecord();

        // Semicolon or tab files (common with some Excel language settings) would otherwise look like one long heading.
        if (records.Count > 0 && records[0].Count == 1 && (records[0][0].Contains(';') || records[0][0].Contains('\t')))
            throw new ImportFileException("The file must use commas between columns.");

        return records;
    }

    /// <summary>
    /// UTF-8 when the bytes are valid UTF-8 (the sample file, "CSV UTF-8" from Excel). Otherwise the file was saved
    /// by Excel as plain "CSV (Comma delimited)" in the Windows character set: read it as Latin-1 so letters such
    /// as é or ñ are kept instead of silently becoming "?" marks.
    /// </summary>
    private static string DecodeText(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    // ---- XLSX -----------------------------------------------------------------------------------------------

    private static List<List<string>> ParseXlsx(MemoryStream memory)
    {
        try
        {
            using var workbook = new XLWorkbook(memory);
            var sheet = workbook.Worksheets.FirstOrDefault();
            var records = new List<List<string>>();
            var lastRow = sheet?.LastRowUsed()?.RowNumber() ?? 0;
            var lastColumn = sheet?.LastColumnUsed()?.ColumnNumber() ?? 0;
            if (sheet == null)
                return records;

            for (var r = 1; r <= lastRow; r++)
            {
                var cells = new List<string>(lastColumn);
                for (var c = 1; c <= lastColumn; c++)
                    cells.Add(CellText(sheet.Cell(r, c)));
                records.Add(cells);
            }

            return records;
        }
        catch (Exception ex) when (ex is not ImportFileException)
        {
            throw new ImportFileException("The Excel file could not be opened. It may be damaged or password protected. Save it again as Excel Workbook (.xlsx) or CSV and try again.");
        }
    }

    /// <summary>The cell as the operator sees it: numbers as plain digits (no 9.19E+11, no thousands separators).</summary>
    private static string CellText(IXLCell cell)
    {
        XLCellValue value;
        try
        {
            value = cell.HasFormula ? cell.CachedValue : cell.Value;
            if (cell.HasFormula && value.IsBlank)
                value = cell.Value;
        }
        catch
        {
            return "";   // a formula Excel itself would not show a value for
        }

        if (value.IsBlank) return "";
        if (value.IsText) return value.GetText();
        if (value.IsBoolean) return value.GetBoolean() ? "TRUE" : "FALSE";
        if (value.IsDateTime) return value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (value.IsNumber)
        {
            var number = value.GetNumber();
            try
            {
                return ((decimal)number).ToString(CultureInfo.InvariantCulture);
            }
            catch (OverflowException)
            {
                return number.ToString("F0", CultureInfo.InvariantCulture);
            }
        }

        return "";   // errors such as #N/A
    }

    // ---- common ---------------------------------------------------------------------------------------------

    /// <summary>Trims cells, drops blank rows (keeping the real row numbers), pads short rows, applies the row limit.</summary>
    private static RawTable ShapeTable(List<List<string>> records)
    {
        if (records.Count == 0)
            return new RawTable([], []);

        var rows = new List<RawRow>();
        for (var i = 1; i < records.Count; i++)
        {
            var cells = records[i].Select(c => c.Trim()).ToList();
            if (cells.All(c => c.Length == 0))
                continue;
            rows.Add(new RawRow(i + 1, cells));
        }

        if (rows.Count > MaxRows)
            throw new ImportFileException($"The file has {rows.Count.ToString("N0", CultureInfo.InvariantCulture)} rows. At most {MaxRows.ToString("N0", CultureInfo.InvariantCulture)} rows can be imported at a time.");

        var headings = records[0].Select(c => c.Trim()).ToList();
        var width = Math.Max(headings.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Cells.Count));
        while (headings.Count < width)
            headings.Add("");

        var padded = rows.Select(r =>
        {
            var cells = r.Cells.ToList();
            while (cells.Count < width)
                cells.Add("");
            return new RawRow(r.RowNumber, cells);
        }).ToList();

        return new RawTable(headings, padded);
    }
}
