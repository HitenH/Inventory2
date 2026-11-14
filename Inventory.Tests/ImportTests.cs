using System.Text;
using ClosedXML.Excel;
using Inventory.Service.Import;

namespace Inventory.Tests;

/// <summary>Batch 8: reading .csv / .xlsx files and matching their headings. No database needed.</summary>
public class ImportTests
{
    internal static MemoryStream Bytes(string text, bool bom = false)
    {
        var body = Encoding.UTF8.GetBytes(text);
        return new MemoryStream(bom ? [0xEF, 0xBB, 0xBF, .. body] : body);
    }

    private static RawTable Csv(string text, bool bom = false) => ImportFileReader.Read(Bytes(text, bom), "file.csv");

    private static string Message(Action action) => Assert.Throws<ImportFileException>(action).Message;

    // ---- CSV ------------------------------------------------------------------------------------------------

    [Fact]
    public void Csv_reads_headings_and_rows_with_excel_row_numbers()
    {
        var table = Csv("Name,Area\r\nA,B\r\nC,D\r\n");
        Assert.Equal(["Name", "Area"], table.Headings);
        Assert.Equal([2, 3], table.Rows.Select(r => r.RowNumber));
        Assert.Equal(["C", "D"], table.Rows[1].Cells);
    }

    [Fact]
    public void Csv_saved_by_excel_in_the_windows_character_set_keeps_its_letters()
    {
        // "José" as Excel's plain "CSV (Comma delimited)" writes it: é is the single byte E9, which is not valid UTF-8.
        byte[] bytes = [.. Encoding.ASCII.GetBytes("Name\r\nJos"), 0xE9, .. Encoding.ASCII.GetBytes("\r\n")];
        var table = ImportFileReader.Read(new MemoryStream(bytes), "file.csv");
        Assert.Equal("José", table.Rows[0].Cells[0]);

        // And real UTF-8 still reads as UTF-8.
        Assert.Equal("José", Csv("Name\r\nJosé\r\n").Rows[0].Cells[0]);
    }

    [Fact]
    public void Csv_handles_quotes_embedded_commas_and_line_breaks()
    {
        var table = Csv("Name,Address\n\"Sharma, \"\"Big\"\" Traders\",\"12 Road\nAndheri\"\n");
        Assert.Equal("Sharma, \"Big\" Traders", table.Rows[0].Cells[0]);
        Assert.Equal("12 Road\nAndheri", table.Rows[0].Cells[1]);
    }

    [Fact]
    public void Csv_with_byte_order_mark_and_lf_line_ends_is_read()
    {
        var table = Csv("Name,Area\nA,B", bom: true);
        Assert.Equal("Name", table.Headings[0]);
        Assert.Equal(["A", "B"], table.Rows[0].Cells);
    }

    [Fact]
    public void Blank_rows_are_skipped_but_real_row_numbers_are_kept()
    {
        var table = Csv("Name,Area\nA,B\n\n , \nC,D\n");
        Assert.Equal([2, 5], table.Rows.Select(r => r.RowNumber));
    }

    [Fact]
    public void Cells_are_trimmed_and_short_rows_are_padded()
    {
        var table = Csv("Name,Area,Remarks\n  A  ,B\n");
        Assert.Equal(["A", "B", ""], table.Rows[0].Cells);
    }

    [Fact]
    public void Semicolon_or_tab_files_get_a_clear_message()
    {
        Assert.Equal("The file must use commas between columns.", Message(() => Csv("Name;Area\nA;B")));
        Assert.Equal("The file must use commas between columns.", Message(() => Csv("Name\tArea\nA\tB")));
    }

    [Fact]
    public void Unclosed_quote_gets_a_clear_message()
    {
        Assert.Contains("never closed", Message(() => Csv("Name,Area\n\"A,B\n")));
    }

    [Fact]
    public void More_than_5000_rows_is_refused()
    {
        var text = "Name\n" + string.Join("\n", Enumerable.Range(0, 5001).Select(i => "n" + i));
        Assert.Equal("The file has 5,001 rows. At most 5,000 rows can be imported at a time.", Message(() => Csv(text)));
        var ok = "Name\n" + string.Join("\n", Enumerable.Range(0, 5000).Select(i => "n" + i));
        Assert.Equal(5000, Csv(ok).Rows.Count);
    }

    [Theory]
    [InlineData("list.xls")]
    [InlineData("list.txt")]
    [InlineData("list")]
    public void Other_file_types_are_refused(string name)
    {
        var message = Message(() => ImportFileReader.Read(Bytes("a,b"), name));
        Assert.Equal("Only .csv and .xlsx files can be imported. Save the file as CSV or Excel Workbook (.xlsx) and try again.", message);
    }

    [Fact]
    public async Task ReadAsync_works_on_a_stream_that_only_allows_async_reads()
    {
        var table = await ImportFileReader.ReadAsync(new AsyncOnlyStream(Bytes("Name\nA")), "FILE.CSV");
        Assert.Equal("A", table.Rows[0].Cells[0]);
    }

    private sealed class AsyncOnlyStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new InvalidOperationException("Synchronous read");
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => inner.ReadAsync(buffer, cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // ---- XLSX -----------------------------------------------------------------------------------------------

    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Sheet1"));
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Xlsx_numbers_come_back_as_plain_digits()
    {
        using var stream = Workbook(s =>
        {
            s.Cell(1, 1).Value = "Mobiles";
            s.Cell(1, 2).Value = "Rate";
            s.Cell(1, 3).Value = "Name";
            s.Cell(2, 1).Value = 919876543210d;
            s.Cell(2, 2).Value = 250.5d;
            s.Cell(2, 3).Value = "  Sharma  ";
            s.Cell(4, 1).Value = 1250000;   // row 3 stays blank
            s.Cell(4, 2).Value = 7;
        });
        var table = ImportFileReader.Read(stream, "list.xlsx");
        Assert.Equal(["Mobiles", "Rate", "Name"], table.Headings);
        Assert.Equal([2, 4], table.Rows.Select(r => r.RowNumber));
        Assert.Equal(["919876543210", "250.5", "Sharma"], table.Rows[0].Cells);
        Assert.Equal(["1250000", "7", ""], table.Rows[1].Cells);
    }

    [Fact]
    public void Xlsx_formulas_give_their_value_and_only_the_first_sheet_is_read()
    {
        using var stream = Workbook(s =>
        {
            s.Cell(1, 1).Value = "Rate";
            s.Cell(2, 1).FormulaA1 = "=100+50";
            s.Workbook.AddWorksheet("Second").Cell(1, 1).Value = "ignored";
        });
        var table = ImportFileReader.Read(stream, "list.xlsx");
        Assert.Equal("150", table.Rows[0].Cells[0]);
    }

    [Fact]
    public void Damaged_workbook_gets_a_clear_message_not_an_exception()
    {
        var message = Message(() => ImportFileReader.Read(Bytes("this is not an excel file"), "broken.xlsx"));
        Assert.Contains("could not be opened", message);
    }

    // ---- heading mapping ------------------------------------------------------------------------------------

    private static MappedTable Map(ImportKind kind, string csv) => ImportChecker.MapHeadings(kind, Csv(csv));

    private static string[] FileMessages(MappedTable mapped) => mapped.FileIssues.Select(i => i.Message).ToArray();

    [Fact]
    public void Headings_match_ignoring_case_spaces_underscores_hyphens_and_a_star()
    {
        foreach (var heading in new[] { "customer id", "CustomerID", "Customer_Id", " CUSTOMER-ID ", "Customer ID *" })
        {
            var mapped = Map(ImportKind.Customers, $"{heading},Name\nC1,A");
            Assert.Empty(mapped.FileIssues);
            Assert.Equal("C1", mapped.Rows[0].Cells[0]);
        }
    }

    [Fact]
    public void Columns_can_be_in_any_order_and_cells_come_back_in_template_order()
    {
        var mapped = Map(ImportKind.Customers, "Mobiles,Name,Customer ID\n9876543210,Sharma,C1");
        Assert.Empty(mapped.FileIssues);
        var cells = mapped.Rows[0].Cells;
        Assert.Equal("C1", cells[0]);
        Assert.Equal("Sharma", cells[1]);
        Assert.Equal("", cells[2]);
        Assert.Equal("9876543210", cells[6]);
    }

    [Fact]
    public void Missing_required_column_is_reported()
    {
        var mapped = Map(ImportKind.Customers, "Name\nA");
        Assert.Equal(["Column 'Customer ID' is missing."], FileMessages(mapped));
        Assert.Null(mapped.FileIssues[0].RowNumber);
    }

    [Fact]
    public void Unknown_heading_is_reported_with_the_allowed_columns()
    {
        var mapped = Map(ImportKind.Customers, "Customer ID,Name,Phone\nC1,A,1");
        var message = Assert.Single(FileMessages(mapped));
        Assert.StartsWith("Column 'Phone' is not recognised. Allowed columns: Customer ID, Name, Contact Person", message);
    }

    [Fact]
    public void Same_column_twice_is_reported()
    {
        var mapped = Map(ImportKind.Customers, "Customer ID,Name,customer_id\nC1,A,C2");
        Assert.Equal(["Column 'Customer ID' appears more than once."], FileMessages(mapped));
    }

    [Fact]
    public void Empty_heading_with_data_is_reported_but_an_empty_trailing_column_is_ignored()
    {
        Assert.Empty(Map(ImportKind.Customers, "Customer ID,Name,\nC1,A,\n").FileIssues);
        var mapped = Map(ImportKind.Customers, "Customer ID,Name,\nC1,A,oops\n");
        Assert.Equal(["Column C has data but no heading. Add a heading or remove the data."], FileMessages(mapped));
    }

    [Fact]
    public void Headings_without_rows_and_empty_files_are_reported()
    {
        Assert.Equal(["The file has headings but no rows."], FileMessages(Map(ImportKind.Customers, "Customer ID,Name\n")));
        Assert.Equal(["The file is empty."], FileMessages(Map(ImportKind.Customers, "")));
    }

    [Fact]
    public void Optional_columns_may_be_absent()
    {
        var mapped = Map(ImportKind.Products, "Product ID,Product Name,Rate\nP1,A,5");
        Assert.Empty(mapped.FileIssues);
        Assert.Equal(8, mapped.Rows[0].Cells.Count);
    }
}
