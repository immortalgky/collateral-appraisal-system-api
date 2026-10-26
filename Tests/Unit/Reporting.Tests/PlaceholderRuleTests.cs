using Reporting.Application.Formatting;
using Shared.DDD;

namespace Reporting.Tests;

/// <summary>
/// The report's address formatter forwards to the shared placeholder rule the appraisal brief also uses
/// (<see cref="PlaceholderText"/>). These pin that forwarding for the report; the house-number rule is
/// pinned in Shared.Tests (DevelopmentOnlyBuildingTests).
/// </summary>
public class PlaceholderRuleTests
{
    [Theory]
    [InlineData("- -", null)]
    [InlineData("\uFF0D", null)]
    [InlineData(" ซอย 5 ", "ซอย 5")]
    public void The_address_formatter_uses_the_shared_rule(string input, string? expected)
    {
        Assert.Equal(expected, ThaiAddressFormatter.Stated(input));
        Assert.Equal(PlaceholderText.Stated(input), ThaiAddressFormatter.Stated(input));
    }

    [Theory]
    [InlineData("–0", false)]   // a dash around a zero is still "not stated"
    [InlineData("0 -", false)]
    [InlineData("7", true)]
    [InlineData("G", true)]
    [InlineData("-", false)]
    public void A_floor_is_stated_unless_it_is_blank_dashes_or_zero(string input, bool expected)
    {
        Assert.Equal(expected, ThaiAddressFormatter.IsStated(input));
    }
}
