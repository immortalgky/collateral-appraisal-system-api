using Shared.DDD;

namespace Shared.Tests.DDD;

public class DevelopmentOnlyBuildingTests
{
    [Theory]
    [InlineData(new[] { false }, true)]            // only improvements
    [InlineData(new[] { false, false }, true)]
    [InlineData(new[] { false, true }, false)]     // any building row keeps it a building
    [InlineData(new[] { true }, false)]
    [InlineData(new bool[0], false)]               // nothing entered yet
    public void Follows_the_appraisal_summary_rule(bool[] isBuildingFlags, bool expected)
    {
        Assert.Equal(expected, DevelopmentOnlyBuilding.IsDevelopmentOnly(isBuildingFlags));
    }

    [Fact]
    public void A_lazy_sequence_is_enumerated_once()
    {
        var count = 0;
        IEnumerable<bool> Flags()
        {
            count++;
            yield return false;
            yield return false;
        }

        Assert.True(DevelopmentOnlyBuilding.IsDevelopmentOnly(Flags()));
        Assert.Equal(1, count);
    }

    // The condo building number still uses the narrow placeholder rule (only - – — at the ends).
    [Theory]
    [InlineData(null, false)]
    [InlineData("-", false)]
    [InlineData(" – ", false)]
    [InlineData("- -", true)]        // a space between the dashes survives the trim
    [InlineData("−", true)]     // minus sign is not one of - – —
    [InlineData("12/3", true)]
    public void HasHouseNumber_reads_a_dash_only_value_as_absent(string? value, bool expected)
    {
        Assert.Equal(expected, DevelopmentOnlyBuilding.HasHouseNumber(value));
    }
}
