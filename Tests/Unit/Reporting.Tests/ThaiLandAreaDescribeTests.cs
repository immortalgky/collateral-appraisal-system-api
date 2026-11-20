using FluentAssertions;
using Reporting.Application.Formatting;
using Reporting.Application.Models;

namespace Reporting.Tests;

/// <summary>The cover line and the 6.x cost summary say the land area the same way: leading zero units dropped, the "(หรือ N ตารางวา)" tail only after a ไร่ or งาน part.</summary>
public class ThaiLandAreaDescribeTests
{
    [Theory]
    [InlineData(43.33, "43.33 ตารางวา")]                                              // only wa: no zero units, no tail
    [InlineData(99.99, "99.99 ตารางวา")]
    [InlineData(243.33, "2 งาน 43.33 ตารางวา (หรือ 243.33 ตารางวา)")]                // no ไร่: no ไร่ part, tail kept
    [InlineData(100, "1 งาน 0 ตารางวา (หรือ 100 ตารางวา)")]
    [InlineData(1066, "2 ไร่ 2 งาน 66 ตารางวา (หรือ 1,066 ตารางวา)")]
    [InlineData(1200, "3 ไร่ 0 งาน 0 ตารางวา (หรือ 1,200 ตารางวา)")]                 // with ไร่ the inner and trailing zeros stay
    [InlineData(400, "1 ไร่ 0 งาน 0 ตารางวา (หรือ 400 ตารางวา)")]
    public void Describe_drops_leading_zero_units_and_the_tail_when_nothing_larger_than_wa_is_printed(double wa, string expected) =>
        ThaiLandAreaFormatter.Describe((decimal)wa).Should().Be(expected);

    [Theory]
    [InlineData(43.33, "43.33 ตร.ว.")]
    [InlineData(243.33, "2 งาน 43.33 ตร.ว. (หรือ 243.33 ตารางวา)")]
    [InlineData(1066, "2 ไร่ 2 งาน 66 ตร.ว. (หรือ 1,066 ตารางวา)")]
    [InlineData(1200, "3 ไร่ 0 งาน 0 ตร.ว. (หรือ 1,200 ตารางวา)")]
    public void The_cost_summary_text_uses_the_short_unit_and_the_same_rule(double wa, string expected) =>
        new SummaryGroupRow { TotalSquareWa = (decimal)wa }.LandAreaText.Should().Be(expected);

    [Fact]
    public void The_cost_summary_text_is_null_without_an_area() => new SummaryGroupRow { TotalSquareWa = 0m }.LandAreaText.Should().BeNull();
}
