using FluentAssertions;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers.Sections;

namespace Reporting.Tests;

/// <summary>Section 8's subject column: factor code → the group's value, and which subject columns a table gets.</summary>
public class ComparisonSubjectTests
{
    private static PropertyGroupDetail Group() => new()
    {
        GroupNumber = 1,
        Lands = [new LandSection { RoadSurfaceType = "คอนกรีต", RoadFrontage = 12.5m, LandFill = "ถมแล้ว", NetAreaInWa = 100m }],
        Buildings =
        [
            new BuildingSection { ModelName = "แบบ A", BuildingAge = 5, BuildingCondition = "ดี", UsableArea = 120m },
            new BuildingSection { ModelName = "แบบ B", BuildingAge = 9, UsableArea = 80.5m },
            new BuildingSection { IsDevelopmentOnly = true, UsableArea = 999m }   // development-only: left out
        ]
    };

    [Fact]
    public void Building_values_come_from_the_first_real_building_and_the_area_is_the_group_total()
    {
        var v = ComparisonSubject.Values(Group(), "ที่ดินพร้อมสิ่งปลูกสร้าง", 918.13m, null);

        v["01"].Should().Be("ที่ดินพร้อมสิ่งปลูกสร้าง");
        v["02"].Should().Be("918.13 ตารางวา");
        v["09"].Should().Be("แบบ A");
        v["13"].Should().Be("5");
        v["14"].Should().Be("200.5");            // 120 + 80.5, development-only excluded
        v["26"].Should().Be("คอนกรีต");
        v["05"].Should().Be("12.5");
        v["03"].Should().Be("ถมแล้ว");
        v.Should().NotContainKey("28");
        v.Should().NotContainKey("32");
    }

    [Fact]
    public void Land_area_falls_back_to_the_lands_net_area_and_condo_reads_the_condo_section()
    {
        ComparisonSubject.Values(Group(), null, null, null)["02"].Should().Be("100 ตารางวา");

        var v = ComparisonSubject.Values(null, "ห้องชุด", null,
            new CondoGroupFigures { UsableArea = 35m, CondoName = "ลุมพินี", BuildingCondition = "ดี", Facilities = "สระว่ายน้ำ" });
        v["14"].Should().Be("35");
        v["18"].Should().Be("ลุมพินี");
        v["11"].Should().Be("ดี");
        v["32"].Should().Be("สระว่ายน้ำ");

        // a condo-only group still says ห้องชุด when the summary row has no type label and no figures came through
        ComparisonSubject.Values(null, null, null, null, condoOnly: true)["01"].Should().Be("ห้องชุด");
        ComparisonSubject.Values(null, null, null, null)["01"].Should().BeNull();
    }

    [Fact]
    public void Numbers_follow_the_groups_first_grid_and_extra_comparables_continue_after_it()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid(), d = Guid.NewGuid();
        Guid m1 = Guid.NewGuid(), m2 = Guid.NewGuid();
        ComparisonSectionLoader.LinkRow L(Guid m, Guid cmp, int seq) => new() { GroupNumber = 1, MethodId = m, MarketComparableId = cmp, DisplaySequence = seq };
        // in the order SQL returns them: by method, then DisplaySequence
        var links = new[] { L(m1, a, 1), L(m1, b, 2), L(m2, c, 1), L(m2, a, 2), L(m2, d, 3) };

        // Every grid header prints the POSITION ("ตลาด {position}"), whatever the method type, so section 8 numbers by position too.
        ComparisonSectionLoader.Numbering(links, [m1, m2])
            .Should().Equal((a, 1), (b, 2), (c, 3), (d, 4));

        // The print order decides, not the order of the rows: when the book prints m2's grid first, m2 fixes the numbers.
        ComparisonSectionLoader.Numbering(links, [m2, m1])
            .Should().Equal((c, 1), (a, 2), (d, 3), (b, 4));

        // DisplaySequence gaps (3, 7) do not leak into the numbers: the grid's columns are 1 and 2.
        var sparse = new[] { L(m1, a, 3), L(m1, b, 7) };
        ComparisonSectionLoader.Numbering(sparse, [m1]).Should().Equal((a, 1), (b, 2));

        // the order given is the order kept (no re-sort by Guid in C#)
        ComparisonSectionLoader.Numbering([L(m1, b, 5), L(m1, a, 5)], [m1]).Should().Equal((b, 1), (a, 2));

        // a method the print order does not list still numbers, after the listed ones
        ComparisonSectionLoader.Numbering(links, [m2]).Should().Equal((c, 1), (a, 2), (d, 3), (b, 4));
    }
}
