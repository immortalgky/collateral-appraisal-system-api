using System.Globalization;
using System.Text.RegularExpressions;
using FluentAssertions;
using NSubstitute;
using Reporting.Application.OperationalReports.Rcas001;
using Reporting.Application.OperationalReports.Rcas007;
using Reporting.Application.OperationalReports.Rcas008;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;
using Reporting.Infrastructure.Export;

namespace Reporting.Tests;

public class TabularExporterPdfLayoutTests
{
    private sealed record Row(string Id, string A, string B, string C, string D, string E);

    private static readonly string Long = new('x', 60);
    private static readonly Row[] Rows = [new("AP-1", Long, Long, Long, Long, "<b>&")];

    private static IReadOnlyList<ReportColumn<Row>> Columns() =>
    [
        new("Name", r => r.A),
        new("Appraisal No.", r => r.Id, Repeat: true),
        new("B", r => r.B),
        new("C", r => r.C),
        new("D", r => r.D),
        new("E", r => r.E),
    ];

    private static string Html(PdfLayout? layout) =>
        TabularExporter.BuildHtml(Rows, Columns(), "T", null, null, layout);

    private static int Count(string haystack, string needle) =>
        (haystack.Length - haystack.Replace(needle, "").Length) / needle.Length;

    [Fact]
    public void Fit_IsDefault_FixedLayoutLandscapeSingleTable()
    {
        var html = Html(null);

        html.Should().Contain("@page{size:A4 landscape;}").And.Contain("table-layout:fixed")
            .And.Contain("overflow-wrap:anywhere");
        Count(html, "<table").Should().Be(1);
        html.Should().NotContain("Columns 1 of");
    }

    [Fact]
    public void Portrait_EmitsPortraitPage()
    {
        Html(new PdfLayout(false, PdfOverflow.Fit, 11)).Should().Contain("@page{size:A4 portrait;}");
    }

    [Fact]
    public void Split_ManyWideColumns_ProducesSeveralTablesEachWithRepeatColumn()
    {
        var html = Html(new PdfLayout(false, PdfOverflow.Split, 11));

        var tables = Count(html, "<table");
        tables.Should().BeGreaterThan(1);
        Count(html, "<th>Appraisal No.</th>").Should().Be(tables);
        html.Should().Contain($"Columns 2 of {tables}");
        // Each non-repeat column appears exactly once across the sets.
        Count(html, "<th>B</th>").Should().Be(1);
        html.Should().Contain("&lt;b&gt;&amp;"); // cell values stay HTML-encoded
    }

    [Fact]
    public void Split_WithoutRepeatColumn_RepeatsNothing()
    {
        IReadOnlyList<ReportColumn<Row>> cols =
            [new("First", r => r.A), new("B", r => r.B), new("C", r => r.C), new("D", r => r.D)];
        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Split, 11));

        Count(html, "<table").Should().BeGreaterThan(1);
        Count(html, "<th>First</th>").Should().Be(1);
    }

    [Fact]
    public void Fit_ColumnGetsAtLeastItsLongestToken()
    {
        // Identifier min = 14 chars * 6.6 + 12 = 104.4px. Six 200-char multi-word columns (natural
        // 32 chars = 223px each) push the sum of naturals past the 1009px page, so the min decides.
        var wrapped = string.Join(' ', Enumerable.Repeat("word", 40));
        List<ReportColumn<Row>> cols = [new("Appraisal No.", r => r.Id)];
        cols.AddRange(Enumerable.Range(0, 6).Select(i => new ReportColumn<Row>($"Remark {i}", _ => wrapped)));
        Row[] rows = [new("AP-2026-000001", "", "", "", "", "")];

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, PdfLayout.Default);

        var pct = double.Parse(Regex.Match(html, "<col style=\"width:([0-9.]+)%\">").Groups[1].Value, CultureInfo.InvariantCulture);
        (pct / 100 * (297 - 30) * 96 / 25.4).Should().BeGreaterThanOrEqualTo(104.4);
    }

    private const double LandscapePx = (297 - 30) * 96 / 25.4;

    private static double TableFont(string html) =>
        double.Parse(Regex.Match(html, @"table\{[^}]*font-size:([0-9.]+)px").Groups[1].Value, CultureInfo.InvariantCulture);

    // <col> widths are % of the table, and the table is itself a % of the printable width.
    private static double[] ColPx(string html, double printablePx)
    {
        var table = double.Parse(Regex.Match(html, "<table style=\"width:([0-9.]+)%\"").Groups[1].Value, CultureInfo.InvariantCulture);
        return Regex.Matches(html, "<col style=\"width:([0-9.]+)%\">")
            .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / 100 * table / 100 * printablePx).ToArray();
    }

    [Fact]
    public void Fit_WideReport_ShrinksTableFontSoMoneyIsNotBroken()
    {
        // 9 money columns (14-char values) + 10 short flags: mins overflow the page at 11px.
        List<ReportColumn<Row>> cols = [];
        cols.AddRange(Enumerable.Range(0, 9).Select(i => new ReportColumn<Row>($"Fee {i}", _ => 820000020m, ColumnFormat.Money)));
        cols.AddRange(Enumerable.Range(0, 10).Select(i => new ReportColumn<Row>($"Flag {i}", _ => "Yes")));

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, PdfLayout.Default);

        var font = TableFont(html);
        font.Should().BeLessThan(11).And.BeGreaterThanOrEqualTo(6);
        ColPx(html, LandscapePx)[0].Should().BeGreaterThanOrEqualTo(14 * 0.6 * font + 12 - 0.1);
        html.Should().Contain("body{margin:0;font-family:'Sarabun','Noto Sans Thai',Arial,sans-serif;font-size:11px");
    }

    [Fact]
    public void Fit_NarrowReport_KeepsChosenFont()
    {
        IReadOnlyList<ReportColumn<Row>> cols = [new("A", r => r.Id), new("B", r => r.Id), new("C", r => r.Id), new("D", r => r.Id), new("E", r => r.Id)];

        TableFont(TabularExporter.BuildHtml(Rows, cols, null, null, null, PdfLayout.Default)).Should().Be(11);
    }

    [Fact]
    public void TotalsValue_CountsTowardMinWidth()
    {
        // Cells are 14 chars ("300,000,000.00") but the total is 16 ("1,200,000,000.00"), bold: 18 chars -> 18*6.6+12 = 130.8px.
        var wrapped = string.Join(' ', Enumerable.Repeat("word", 40));
        List<ReportColumn<Row>> cols = [new("Amount", _ => 300000000m, ColumnFormat.Money, Total: true)];
        cols.AddRange(Enumerable.Range(0, 6).Select(i => new ReportColumn<Row>($"Remark {i}", _ => wrapped)));
        Row[] rows = [Rows[0], Rows[0], Rows[0], Rows[0]];

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, PdfLayout.Default);

        html.Should().Contain("1,200,000,000.00");
        TableFont(html).Should().Be(11);
        ColPx(html, LandscapePx)[0].Should().BeGreaterThanOrEqualTo(18 * 0.6 * 11 + 12 - 0.1);
    }

    [Fact]
    public void Split_TotalsLabelWidthCountsWhenPacking()
    {
        // Portrait page = 680px. Without the label the set is N 25.2 + 3 x 190.2 + Amt 71.4 = 667.2px (fits);
        // the "Total (1000)" label (bold) widens N to 104.4px (-> 746px), so the packer must split.
        List<ReportColumn<Row>> cols = [new("N", _ => "1", Repeat: true)];
        cols.AddRange(Enumerable.Range(0, 3).Select(i => new ReportColumn<Row>($"C{i}", _ => new string('x', 27))));
        cols.Add(new("Amt", _ => 1m, ColumnFormat.Money, Total: true));
        var rows = Enumerable.Repeat(Rows[0], 1000).ToArray();

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Split, 11));

        Count(html, "<table").Should().Be(2);
        Count(html, "Total (1000)").Should().Be(1);
    }

    [Fact]
    public void NonTextCells_AreNowrap_TextCellsAreNot()
    {
        List<ReportColumn<Row>> cols =
        [
            new("Name", r => r.Id),
            new("Fee", _ => 3745m, ColumnFormat.Money, Total: true),
            new("Count", _ => 7, ColumnFormat.Integer),
            new("At", _ => new DateTime(2026, 1, 2, 3, 4, 0), ColumnFormat.DateTime),
        ];

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, null);

        html.Should().Contain("td.nw{white-space:nowrap}")
            .And.Contain("<td>AP-1</td>")
            .And.Contain("<td class=\"num nw\">3,745.00</td>")
            .And.Contain("<td class=\"num nw\">7</td>")
            .And.Contain("<td class=\"nw\">2026-01-02 03:04</td>");
        // totals row: the total cell is nowrap too (one in the body row, one in the totals row)
        Count(html, "<td class=\"num nw\">3,745.00</td>").Should().Be(2);
    }

    [Fact]
    public void Fit_AtFontFloor_NumbersKeepTheirMinAndTextAbsorbsTheShrinkDownToItsFloor()
    {
        // 8 money + 3 datetime nowrap columns (708px at 6px) leave room for the five 40-char-token Text
        // columns only below their min (16 chars = 69.6px), but above their floor (3 chars = 22.8px).
        var token = new string('q', 40);
        List<ReportColumn<Row>> cols = [];
        cols.AddRange(Enumerable.Range(0, 8).Select(i => new ReportColumn<Row>($"Fee {i}", _ => 820000020m, ColumnFormat.Money)));
        cols.AddRange(Enumerable.Range(0, 3).Select(i => new ReportColumn<Row>($"At {i}", _ => new DateTime(2026, 1, 2, 3, 4, 0), ColumnFormat.DateTime)));
        cols.AddRange(Enumerable.Range(0, 5).Select(i => new ReportColumn<Row>($"Text {i}", _ => token)));

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, PdfLayout.Default);

        var font = TableFont(html);
        font.Should().Be(6);
        html.Should().Contain("<td class=\"num nw\">");
        var px = ColPx(html, LandscapePx);
        for (var i = 0; i < 8; i++) px[i].Should().BeGreaterThanOrEqualTo(14 * 0.6 * font + 12 - 0.1);
        for (var i = 8; i < 11; i++) px[i].Should().BeGreaterThanOrEqualTo(16 * 0.6 * font + 12 - 0.1);
        for (var i = 11; i < 16; i++) px[i].Should().BeInRange(3 * 0.6 * font + 12 - 0.1, 16 * 0.6 * font + 12 - 1);
    }

    [Fact]
    public void Fit_NowrapMinsBeyondPage_DropNowrapAndScaleEverything()
    {
        // 14 money columns need 873px at 6px but a portrait page has 680px: wrapping a number beats overlapping.
        var cols = Enumerable.Range(0, 14).Select(i => new ReportColumn<Row>($"Fee {i}", _ => 820000020m, ColumnFormat.Money)).ToList();

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11));

        TableFont(html).Should().Be(6);
        html.Should().Contain("<td class=\"num\">").And.NotContain("class=\"num nw\"");
        ColPx(html, 180 * 96 / 25.4).Sum().Should().BeApproximately(180 * 96 / 25.4, 1);
    }

    [Fact]
    public void NonTextColumn_NeverNarrowerThanItsValue_EvenAboveNaturalCap()
    {
        IReadOnlyList<ReportColumn<Row>> cols = [new("Name", r => r.Id), new("At", _ => new string('d', 40), ColumnFormat.DateTime)];

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, null);

        ColPx(html, LandscapePx)[1].Should().BeGreaterThanOrEqualTo(40 * 0.6 * 11 + 12 - 0.1);
    }

    private const double PortraitPx = 180 * 96 / 25.4;

    [Fact]
    public void Fit_ThaiLongRemarks_DoNotShrinkTheFont()
    {
        // No spaces in Thai: a whole sentence is one token, but only 16 chars of it count toward the min width.
        var thai = new string('ก', 60);
        var cols = Enumerable.Range(0, 6).Select(i => new ReportColumn<Row>($"Remark {i}", _ => thai)).ToList();

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, PdfLayout.Default);

        TableFont(html).Should().Be(11);
        ColPx(html, LandscapePx).Should().OnlyContain(w => w < 32 * 0.6 * 11 + 12);
    }

    [Fact]
    public void NumericColumn_SpaceFreeThaiHeader_CountsOnlyTheCapTowardMin()
    {
        // 8 one-digit columns, 20-char space-free header: min 16 chars (117.6px at 11px) fits 1009px, an
        // uncapped 22-char bold header (157.2px each) would not and would shrink the font.
        var header = new string('ก', 20);
        var cols = Enumerable.Range(0, 8).Select(i => new ReportColumn<Row>(header, _ => 5, ColumnFormat.Integer)).ToList();

        TableFont(TabularExporter.BuildHtml(Rows, cols, null, null, null, PdfLayout.Default)).Should().Be(11);
    }

    [Fact]
    public void Rcas007_PortraitFit11_KeepsNumbersAndDatesNowrap()
    {
        // Real column set; mostly-empty Text columns must not squeeze the numbers into the wrapping fallback.
        var cols = SlaReport.Create("RCAS007-x", "T", null, Substitute.For<IReportSlaCalculator>()).Columns;
        Rcas007Row[] rows =
        [
            new()
            {
                AppraisalNumber = "AP-2026-000123", CustomerName = "Customer Name", AppraisalFee = 3745m,
                AppraisalCreateDate = new DateTime(2026, 2, 24, 1, 48, 0), AppointmentDate = new DateTime(2026, 2, 26, 9, 0, 0),
                Sla = 193.79m, AppraisalValue = 1500000m, Role = "Admin", AppraisalStatus = "Completed",
            },
            new() { AppraisalNumber = "AP-2026-000124", AppraisalFee = 3745m, Sla = 1.5m, AppraisalValue = 2500000m },
        ];

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11));

        html.Should().Contain("<td class=\"nw\">2026-02-24 01:48</td>").And.Contain("<td class=\"num nw\">3,745.00</td>");
        var font = TableFont(html);
        var px = ColPx(html, PortraitPx);
        (int Chars, string Header)[] want =
            [(8, "Appraisal Fee"), (16, "Created Date"), (16, "Appointment Date"), (6, "SLA (days)"), (12, "Appraisal Value")];
        foreach (var (chars, header) in want)
            px[cols.ToList().FindIndex(c => c.Header == header)].Should().BeGreaterThanOrEqualTo(chars * 0.6 * font + 12 - 0.1, header);
    }

    [Fact]
    public void Rcas001_PortraitFit_AppraisalNumberIsNowrapAndFitsItsValue()
    {
        var cols = Rcas001Report.Definition.Columns;
        var name = string.Join(' ', Enumerable.Repeat("Customer", 6));
        var rows = Enumerable.Range(1, 3).Select(i => new Rcas001Row(
            Guid.NewGuid(), new DateTime(2026, 2, 24), $"AP-2026-00012{i}", name, "Purpose", 1500000m, name, name, 2500000m,
            "Completed", "P5229", "Dept", "Segment", name, name, new DateTime(2026, 3, 1)) { RunningNo = i }).ToArray();

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11));

        html.Should().Contain("<td class=\"nw\">AP-2026-000121</td>");
        ColPx(html, PortraitPx)[cols.ToList().FindIndex(c => c.Header == "Appraisal No.")]
            .Should().BeGreaterThanOrEqualTo(14 * 0.6 * TableFont(html) + 12 - 0.1);
    }

    [Fact]
    public void Rcas008_ThaiLandscapeFit_AppraisalNumberIsNowrapAndFitsItsValue()
    {
        var cols = ReportLanguage.Th.Headers(Rcas008Report.Definition.Columns);
        var remark = string.Join(' ', Enumerable.Repeat("remark", 12));
        var rows = Enumerable.Range(1, 3).Select(i => new Rcas008Row(
            Guid.NewGuid(), $"AP-2026-00012{i}", "Company", "Company", new DateTime(2026, 3, 1), "Segment", 85.5m, 4, 5, 3, 4, 5,
            remark, "Done", "Staff", "01", "New", remark, remark, remark, remark, remark, "Segment", null)).ToArray();

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, PdfLayout.Default);

        html.Should().Contain("AP-2026-000121</td>").And.Contain("class=\"nw\"");
        ColPx(html, LandscapePx)[cols.ToList().FindIndex(c => c.Repeat)]
            .Should().BeGreaterThanOrEqualTo(14 * 0.6 * TableFont(html) + 12 - 0.1);
    }

    [Fact]
    public void SpareRoom_GoesToTextColumnsBeforeNowrapColumnsGrow()
    {
        // Mins: money 10 chars (header word) = 78px + 3 Text x 58.2px (bold "Remark") = 252.6px of 680px. The 427px
        // of spare room all fits in the Text columns (165px each below natural 223px), so money stays at its min.
        var words = string.Join(' ', Enumerable.Repeat("word", 40));
        List<ReportColumn<Row>> cols = [new("Appraisal Amount Total", _ => 5m, ColumnFormat.Money)];
        cols.AddRange(Enumerable.Range(0, 3).Select(i => new ReportColumn<Row>($"Remark {i}", _ => words)));

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11));

        var px = ColPx(html, PortraitPx);
        px[0].Should().BeApproximately(10 * 0.6 * 11 + 12, 0.5);
        px.Skip(1).Should().OnlyContain(w => w > 190);
    }

    [Fact]
    public void HeaderDominatedColumn_NaturalIsItsLongestWord()
    {
        // "Appraisal Company Name": the header wraps, so only "Appraisal" counts: bold 9 -> 10 chars = 78px (a
        // nowrap column, so the leftover page space goes to the Text column next to it instead).
        IReadOnlyList<ReportColumn<Row>> cols = [new("Appraisal Company Name", _ => 5m, ColumnFormat.Money), new("Note", r => r.A)];

        var px = ColPx(TabularExporter.BuildHtml(Rows, cols, null, null, null, null), LandscapePx)[0];

        px.Should().BeApproximately(10 * 0.6 * 11 + 12, 0.2);
    }

    [Fact]
    public void Rcas007_PortraitSplit11_HeaderWordsLetSegmentFitTheSet()
    {
        // Before headers counted as one longest word the same data needed 4 sets ("Banking Segment" natural was
        // its whole bold header, ~121px, and spilled to the next set).
        var cols = SlaReport.Create("RCAS007-x", "T", null, Substitute.For<IReportSlaCalculator>()).Columns;
        var rows = Enumerable.Range(1, 40).Select(i => new Rcas007Row
        {
            AppraisalNumber = $"AP-2026-0001{i:00}", CustomerName = "Somchai Jaidee Co Ltd", Purpose = "Loan", RequestorName = "Somsak R",
            RequestorPhone = "02-123-4567", RequestorDepartment = "Retail", BankingSegment = "RETAIL", AppraisalCompany = "ABC Appraisal",
            ExternalStaffName = "Somying", AppraisalCompanyPhone = "02-111-2222", InternalAppraisalStaff = "Staff A",
            InternalAppraisalStaffPhone = "02-333-4444", AppraisalFee = 3745m, AppraisalCreateDate = new DateTime(2026, 2, 24, 1, 48, 0),
            AppointmentDate = new DateTime(2026, 2, 26, 9, 0, 0), Sla = 193.79m, AppraisalValue = 2500000m, Role = "Admin", AppraisalStatus = "Pending",
        }).ToArray();

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Split, 11));

        Count(html, "<table").Should().Be(3);
    }

    [Fact]
    public void LatinHeaderWord_IsNotCappedAt16Chars()
    {
        // 8 Text columns whose header is one 17-char word: bold 19 chars = 137.4px each = 1099px > 1009px, so the
        // font shrinks. (Capped at 16 they would be 941px and the font would stay 11.)
        var cols = Enumerable.Range(0, 8).Select(i => new ReportColumn<Row>(new string('W', 17), _ => "a")).ToList();

        TableFont(TabularExporter.BuildHtml(Rows, cols, null, null, null, PdfLayout.Default)).Should().BeLessThan(11);
    }

    [Fact]
    public void NbspJoinedHeader_IsOneToken()
    {
        // "Appraisal Amount Total" joined by NBSP is 22 chars, bold 25: a nowrap column holds it whole.
        IReadOnlyList<ReportColumn<Row>> cols =
            [new("Appraisal\u00A0Amount\u00A0Total", _ => 5m, ColumnFormat.Money), new("Note", r => r.A)];

        var px = ColPx(TabularExporter.BuildHtml(Rows, cols, null, null, null, null), LandscapePx)[0];

        px.Should().BeApproximately(25 * 0.6 * 11 + 12, 0.2);
    }

    [Fact]
    public void Want_ComesFromCellValuesNotHeaders()
    {
        // Both columns have a natural width of 32 chars. Only "Long" holds content beyond that (60 chars), so it
        // takes the Want share first; the 40-char header word of "Head" gives it no Want.
        Row[] rows = [new("AP-1", "ab", Long, "", "", "")];
        IReadOnlyList<ReportColumn<Row>> cols = [new(new string('H', 40), r => r.A), new("Long", r => r.B)];

        var px = ColPx(TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11)), PortraitPx);

        px[1].Should().BeGreaterThan(px[0] + 28 * 0.6 * 11 - 1);
    }

    [Fact]
    public void TextColumn_HeaderLongerThanValues_GetsBoldHeaderWidth()
    {
        IReadOnlyList<ReportColumn<Row>> cols = [new("Description", _ => "ab")];

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, null);

        ColPx(html, LandscapePx)[0].Should().BeGreaterThanOrEqualTo(11 * 1.1 * 0.6 * 11 + 12 - 0.1);
    }

    [Fact]
    public void Fallback_TextColumnsKeepTheirFloorAndNowrapIsScaled()
    {
        List<ReportColumn<Row>> cols = [];
        cols.AddRange(Enumerable.Range(0, 14).Select(i => new ReportColumn<Row>($"Fee {i}", _ => 820000020m, ColumnFormat.Money)));
        cols.AddRange(Enumerable.Range(0, 2).Select(i => new ReportColumn<Row>($"Text {i}", _ => new string('q', 40))));

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11));

        TableFont(html).Should().Be(6);
        html.Should().NotContain("class=\"num nw\"");
        var px = ColPx(html, PortraitPx);
        px[14].Should().BeApproximately(3 * 0.6 * 6 + 12, 0.2);
        px[15].Should().BeApproximately(3 * 0.6 * 6 + 12, 0.2);
        px.Take(14).Should().OnlyContain(w => w < 14 * 0.6 * 6 + 12);
        px.Sum().Should().BeApproximately(PortraitPx, 1);
    }

    [Fact]
    public void LeftoverSpace_FirstGoesToTextColumnLongerThanNaturalCap()
    {
        // ID 104.4px, 3 short columns 31.8px, long text natural 223.2px: 422px of 680px used.
        Row[] rows = [new("AP-2026-000001", Long, "Yes", "Yes", "Yes", "")];
        IReadOnlyList<ReportColumn<Row>> cols =
            [new("Appraisal No.", r => r.Id, Repeat: true), new("Name", r => r.A), new("B", r => r.B), new("C", r => r.C), new("D", r => r.D)];

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Split, 11));

        var px = ColPx(html, PortraitPx);
        px[0].Should().BeApproximately(14 * 0.6 * 11 + 12, 0.2); // ID at its natural width, not stretched
        px.Sum().Should().BeApproximately(PortraitPx, 1); // Text columns fill the page
        // The 60-char column got its extra 28 chars first, so it is the widest; short columns grow only with the rest.
        px[1].Should().BeGreaterThan(32 * 0.6 * 11 + 12 + 28 * 0.6 * 11 - 0.5);
        for (var i = 2; i < 5; i++) px[i].Should().BeGreaterThan(3 * 0.6 * 11 + 12).And.BeLessThan(px[1]);
    }

    [Fact]
    public void SetWithoutTextColumns_NumbersShareTheLeftoverAndIdKeepsItsWidth()
    {
        Row[] rows = [new("AP-2026-000001", "Yes", "Yes", "Yes", "Yes", "")];
        IReadOnlyList<ReportColumn<Row>> cols =
            [new("Appraisal No.", r => r.Id, Repeat: true), new("Fee", _ => 5m, ColumnFormat.Money), new("Qty", _ => 7, ColumnFormat.Integer)];

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Fit, 11));

        html.Should().Contain("width:100%\"><colgroup>");
        var px = ColPx(html, PortraitPx);
        px.Sum().Should().BeApproximately(PortraitPx, 1);
        px[0].Should().BeApproximately(14 * 0.6 * 11 + 12, 0.2);
        px[1].Should().BeGreaterThan(200);
        px[2].Should().BeGreaterThan(200);
    }

    [Fact]
    public void LeftoverSpace_TextColumnsFillThePageWhileIdentifierKeepsItsWidth()
    {
        Row[] rows = [new("AP-2026-000001", "Somchai", "02-123-4567", "Retail", "", "")];
        IReadOnlyList<ReportColumn<Row>> cols =
            [new("Appraisal No.", r => r.Id, Repeat: true), new("Requestor", r => r.A), new("Requestor Phone", r => r.B), new("Requestor Dept.", r => r.C)];

        var html = TabularExporter.BuildHtml(rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Split, 11));

        var px = ColPx(html, PortraitPx);
        px[0].Should().BeApproximately(14 * 0.6 * 11 + 12, 0.2);
        px.Sum().Should().BeApproximately(PortraitPx, 1);
        px.Skip(1).Should().OnlyContain(w => w > 100);
    }

    [Fact]
    public void Split_LongCell_WrapsInsteadOfGettingItsOwnSet()
    {
        IReadOnlyList<ReportColumn<Row>> cols =
            [new("A", r => r.Id), new("B", r => r.Id), new("Remark", r => new string('z', 200) + " " + new string('w', 10))];

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, new PdfLayout(false, PdfOverflow.Split, 11));

        Count(html, "<table").Should().Be(1);
    }

    [Fact]
    public void Totals_AreTheLastBodyRow_NotTfoot()
    {
        IReadOnlyList<ReportColumn<Row>> cols = [new("Name", r => r.A), new("Amount", r => 5m, ColumnFormat.Money, Total: true)];

        var html = TabularExporter.BuildHtml(Rows, cols, null, null, null, null);

        html.Should().NotContain("<tfoot");
        html.Should().Contain("<tr class=\"total\">").And.Contain("Total (1)");
        html.IndexOf("<tr class=\"total\">", StringComparison.Ordinal)
            .Should().BeLessThan(html.IndexOf("</tbody>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("500", 16)]
    [InlineData("99999999999", 16)]
    [InlineData("99999999999999999999999", 16)]
    [InlineData("-5", 6)]
    [InlineData("1", 6)]
    [InlineData("abc", 11)]
    [InlineData(null, 11)]
    [InlineData("9", 9)]
    public void FontSize_IsClampedAndDefaults(string? input, int expected)
    {
        PdfLayoutQuery.Parse(null, null, input).Value.FontSizePx.Should().Be(expected);
    }

    [Theory]
    [InlineData("PORTRAIT", "split", false, PdfOverflow.Split)]
    [InlineData("nonsense", "nonsense", true, PdfOverflow.Fit)]
    [InlineData(null, null, true, PdfOverflow.Fit)]
    public void Query_UnknownValuesFallBackToDefaults(string? orientation, string? overflow, bool landscape, PdfOverflow mode)
    {
        var layout = PdfLayoutQuery.Parse(orientation, overflow, null).Value;

        layout.Landscape.Should().Be(landscape);
        layout.Overflow.Should().Be(mode);
    }
}
