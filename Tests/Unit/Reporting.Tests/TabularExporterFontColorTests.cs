using ClosedXML.Excel;
using FluentAssertions;
using NSubstitute;
using Reporting.Application.Services;
using Reporting.Contracts;
using Reporting.Infrastructure.Export;
using Shared.Time;

namespace Reporting.Tests;

public class TabularExporterFontColorTests
{
    private static readonly string[] Rows = ["A1"];

    private static ReportColumn<string>[] Columns(string? color) =>
        [new("No.", s => s, FontColor: color), new("Other", s => s + "x")];

    private static TabularExporter Exporter(IPdfRenderer? pdf = null) =>
        new(pdf ?? Substitute.For<IPdfRenderer>(), Substitute.For<IDateTimeProvider>());

    [Fact]
    public async Task Xlsx_data_cells_of_a_coloured_column_get_the_font_colour_and_others_do_not()
    {
        var file = await Exporter().ExportAsync(Rows, Columns("#0d9488"), "T-x", ReportFormat.Xlsx);

        using var wb = new XLWorkbook(new MemoryStream(file.Bytes));
        var ws = wb.Worksheet(1);
        var data = ws.CellsUsed().Single(c => c.GetString() == "A1");
        data.Style.Font.FontColor.Color.ToArgb().Should().Be(XLColor.FromHtml("#0d9488").Color.ToArgb());
        ws.CellsUsed().Single(c => c.GetString() == "A1x").Style.Font.FontColor.Color.ToArgb()
            .Should().NotBe(XLColor.FromHtml("#0d9488").Color.ToArgb());
        ws.CellsUsed().Single(c => c.GetString() == "No.").Style.Font.FontColor.Color.ToArgb()
            .Should().NotBe(XLColor.FromHtml("#0d9488").Color.ToArgb());
    }

    [Fact]
    public async Task Pdf_html_carries_an_inline_colour_style_on_the_coloured_column_only()
    {
        string? html = null;
        var pdf = Substitute.For<IPdfRenderer>();
        pdf.RenderAsync(Arg.Do<string>(h => html = h), Arg.Any<CancellationToken>()).Returns([1]);

        await Exporter(pdf).ExportAsync(Rows, Columns("#0d9488"), "T-x", ReportFormat.Pdf);

        html.Should().Contain("<td style=\"color:#0d9488\">A1</td>").And.Contain("<td>A1x</td>");
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#0d9488;background:url(x)")]
    [InlineData("#0d9488\n")]
    public async Task Invalid_colour_is_ignored(string bad)
    {
        string? html = null;
        var pdf = Substitute.For<IPdfRenderer>();
        pdf.RenderAsync(Arg.Do<string>(h => html = h), Arg.Any<CancellationToken>()).Returns([1]);

        await Exporter(pdf).ExportAsync(Rows, Columns(bad), "T-x", ReportFormat.Pdf);

        html.Should().NotContain("style=\"color");
    }

    [Fact]
    public async Task Xlsx_truncates_a_string_longer_than_the_cell_limit()
    {
        var file = await Exporter().ExportAsync([new string('x', 40_000)], Columns(null), "T-x", ReportFormat.Xlsx);

        using var wb = new XLWorkbook(new MemoryStream(file.Bytes));
        wb.Worksheet(1).CellsUsed().Max(c => c.GetString().Length).Should().Be(32_767);
    }

    private static ReportColumn<string>[] CenteredColumns() =>
        [new("No.", s => s, FontColor: "#0d9488", Center: true), new("Other", s => s + "x")];

    [Fact]
    public async Task Xlsx_centers_header_and_data_of_a_centered_column_only()
    {
        var file = await Exporter().ExportAsync(Rows, CenteredColumns(), "T-x", ReportFormat.Xlsx);

        using var wb = new XLWorkbook(new MemoryStream(file.Bytes));
        var cells = wb.Worksheet(1).CellsUsed();
        foreach (var text in new[] { "No.", "A1" })
            cells.Single(c => c.GetString() == text).Style.Alignment.Horizontal.Should().Be(XLAlignmentHorizontalValues.Center);
        foreach (var text in new[] { "Other", "A1x" })
            cells.Single(c => c.GetString() == text).Style.Alignment.Horizontal.Should().NotBe(XLAlignmentHorizontalValues.Center);
        // Every header/data cell is top-aligned, centered or not.
        foreach (var text in new[] { "No.", "A1", "Other", "A1x" })
            cells.Single(c => c.GetString() == text).Style.Alignment.Vertical.Should().Be(XLAlignmentVerticalValues.Top);
    }

    [Fact]
    public async Task Pdf_centers_a_centered_column_with_a_single_merged_style_attribute()
    {
        string? html = null;
        var pdf = Substitute.For<IPdfRenderer>();
        pdf.RenderAsync(Arg.Do<string>(h => html = h), Arg.Any<CancellationToken>()).Returns([1]);

        await Exporter(pdf).ExportAsync(Rows, CenteredColumns(), "T-x", ReportFormat.Pdf);

        html.Should().Contain("<th style=\"text-align:center\">No.</th>")
            .And.Contain("<td style=\"color:#0d9488;text-align:center\">A1</td>")
            .And.Contain("<th>Other</th>").And.Contain("<td>A1x</td>");
    }

    // RCAS008 shape: Total on the text first column (record count only), a centered column after it.
    private static ReportColumn<string>[] TotalColumns() =>
        [new("No.", s => s, FontColor: "#0d9488", Total: true), new("Seg", s => s + "x", Center: true)];

    [Fact]
    public async Task Xlsx_footer_shows_the_record_count()
    {
        var file = await Exporter().ExportAsync(["A1", "A2"], TotalColumns(), "T-x", ReportFormat.Xlsx);

        using var wb = new XLWorkbook(new MemoryStream(file.Bytes));
        var total = wb.Worksheet(1).CellsUsed().Single(c => c.GetString() == "Total (2)");
        total.Address.ColumnNumber.Should().Be(1);
        total.Style.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public async Task Pdf_footer_shows_the_record_count_and_leaves_the_empty_footer_cell_unstyled()
    {
        string? html = null;
        var pdf = Substitute.For<IPdfRenderer>();
        pdf.RenderAsync(Arg.Do<string>(h => html = h), Arg.Any<CancellationToken>()).Returns([1]);

        await Exporter(pdf).ExportAsync(["A1", "A2"], TotalColumns(), "T-x", ReportFormat.Pdf);

        html.Should().Contain("<tfoot><tr><td>Total (2)</td><td></td></tr></tfoot>")
            .And.Contain("tfoot{display:table-row-group}"); // prints once at the end, not on every page
    }
}
