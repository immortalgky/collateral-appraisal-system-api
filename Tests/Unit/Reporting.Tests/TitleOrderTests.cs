using FluentAssertions;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>
/// Titles of one property print in the order the user entered them (RS13 ORDER BY). They share
/// SequenceInGroup, so re-sorting in C# by TitleId would scramble them.
/// </summary>
public class TitleOrderTests
{
    private static AppraisalSummaryLandBuildingDataProvider.GroupTitleRow Row(Guid group, string titleId) =>
        new() { PropertyGroupId = group, SequenceInGroup = 1, TitleId = new Guid(titleId) };

    [Fact]
    public void TitlesByGroup_KeepsSqlOrder_NotTitleIdOrder()
    {
        // One group arrives with ascending TitleIds, the other descending: any TitleId sort breaks one.
        var asc = Guid.NewGuid();
        var desc = Guid.NewGuid();
        var a1 = Row(asc, "00000000-0000-0000-0000-000000000001");
        var a2 = Row(asc, "00000000-0000-0000-0000-000000000002");
        var d1 = Row(desc, "00000000-0000-0000-0000-000000000002");
        var d2 = Row(desc, "00000000-0000-0000-0000-000000000001");

        var result = AppraisalSummaryLandBuildingDataProvider.TitlesByGroup([a1, d1, a2, d2]);

        result[asc].Should().Equal(a1, a2);
        result[desc].Should().Equal(d1, d2);
    }
}
