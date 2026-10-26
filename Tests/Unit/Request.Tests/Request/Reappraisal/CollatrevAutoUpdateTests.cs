using Integration.FileInterface.Format.Reappraisal;

namespace Request.Tests.Request.Reappraisal;

/// <summary>The dev generator's <c>autoUpdate</c> input: Y or N in any case, blank leaves position 650 blank.</summary>
public class CollatrevAutoUpdateTests
{
    [Theory]
    [InlineData("Y", "Y")]
    [InlineData("y", "Y")]
    [InlineData("N", "N")]
    [InlineData(" n ", "N")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    public void Accepts_Y_N_or_blank(string? input, string? expected)
    {
        Assert.True(CollatrevAutoUpdate.TryNormalize(input, out var flag));
        Assert.Equal(expected, flag);
    }

    [Theory]
    [InlineData("X")]
    [InlineData("YES")]
    [InlineData("1")]
    public void Rejects_anything_else(string input)
        => Assert.False(CollatrevAutoUpdate.TryNormalize(input, out _));
}
