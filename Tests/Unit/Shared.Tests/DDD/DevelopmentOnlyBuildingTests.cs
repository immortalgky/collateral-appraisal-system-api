using Shared.DDD;

namespace Shared.Tests.DDD;

public class DevelopmentOnlyBuildingTests
{
    [Theory]
    [InlineData(null, new[] { false }, true)]          // no house number, only improvements
    [InlineData("-", new[] { false, false }, true)]    // a placeholder dash is not a house number
    [InlineData(" – ", new[] { false }, true)]
    // The report's ORIGINAL rule is narrow on purpose (only - – — at the ends): these other placeholder
    // shapes still count as a house number, exactly as before this branch.
    [InlineData("- -", new[] { false }, false)]        // a space between the dashes survives the trim
    [InlineData("\u2212", new[] { false }, false)]      // minus sign is not one of - – —
    [InlineData("\uFF0D", new[] { false }, false)]      // fullwidth hyphen-minus neither
    [InlineData("12/3", new[] { false }, false)]       // a real house number keeps it a building
    [InlineData(null, new[] { false, true }, false)]   // any building row keeps it a building
    [InlineData(null, new bool[0], false)]             // nothing entered yet
    public void Follows_the_appraisal_summary_rule(string? houseNumber, bool[] isBuildingFlags, bool expected)
    {
        Assert.Equal(expected, DevelopmentOnlyBuilding.IsDevelopmentOnly(houseNumber, isBuildingFlags));
    }
}
