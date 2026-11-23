using FluentAssertions;
using Reporting.Application.OperationalReports.Shared;
using Reporting.Contracts;

namespace Reporting.Tests;

public class ReportLanguageTests
{
    [Theory]
    [InlineData("th", ReportLanguage.Th)]
    [InlineData("TH", ReportLanguage.Th)]
    [InlineData("en", ReportLanguage.En)]
    [InlineData(null, ReportLanguage.En)]
    [InlineData("garbage", ReportLanguage.En)]
    public void Parse_is_lenient_and_defaults_to_English(string? lang, ReportLanguage expected) =>
        OperationalReportLanguage.Parse(lang).Should().Be(expected);

    private static readonly IReadOnlyList<ReportColumn<string>> Columns =
    [
        new("Name", s => s, HeaderTh: "ชื่อ"),
        new("Remark", s => s),
    ];

    [Fact]
    public void Thai_headers_fall_back_to_English_when_HeaderTh_is_null() =>
        ReportLanguage.Th.Headers(Columns).Select(c => c.Header).Should().Equal("ชื่อ", "Remark");

    [Fact]
    public void English_keeps_the_columns_untouched() =>
        ReportLanguage.En.Headers(Columns).Should().BeSameAs(Columns);

    [Fact]
    public void Thai_filter_labels_fall_back_to_English_and_English_is_untouched()
    {
        IReadOnlyList<FilterField> fields = [new("Purpose", "1", LabelTh: "วัตถุประสงค์"), new("Other", "x")];

        ReportLanguage.Th.Labels(fields).Select(f => f.Label).Should().Equal("วัตถุประสงค์", "Other");
        ReportLanguage.En.Labels(fields).Should().BeSameAs(fields);
    }
}
