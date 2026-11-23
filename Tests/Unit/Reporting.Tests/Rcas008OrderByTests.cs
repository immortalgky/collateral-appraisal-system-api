using FluentAssertions;
using Reporting.Application.OperationalReports.Rcas008;

namespace Reporting.Tests;

public class Rcas008OrderByTests
{
    private static string OrderBy(string? sortBy, string? sortDir) =>
        Rcas008Report.Definition.OrderBy(new Rcas008Filter(null, null, null, null, null, null, null, null, sortBy, sortDir));

    [Fact]
    public void Default_sort_gets_the_tiebreaker() =>
        OrderBy(null, null).Should().Be("AppraisalCompany ASC, ApprovedDate ASC, AppraisalNumber ASC, Id ASC");

    [Fact]
    public void Allowed_column_with_desc_keeps_direction_and_tiebreaker_stays_ascending() =>
        OrderBy("InternalAppraisalStaff", "desc").Should().Be("InternalAppraisalStaff DESC, AppraisalNumber ASC, Id ASC");

    [Fact]
    public void Sorting_by_AppraisalNumber_does_not_repeat_it() =>
        OrderBy("AppraisalNumber", "desc").Should().Be("AppraisalNumber DESC, Id ASC");

    [Fact]
    public void Unknown_sort_falls_back_to_default_plus_tiebreaker() =>
        OrderBy("x; DROP TABLE y", "desc")
            .Should().Be("AppraisalCompany DESC, ApprovedDate DESC, AppraisalNumber ASC, Id ASC");
}
