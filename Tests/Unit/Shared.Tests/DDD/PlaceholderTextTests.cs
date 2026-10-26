using Shared.DDD;

namespace Shared.Tests.DDD;

public class PlaceholderTextTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("-", null)]
    [InlineData(" – ", null)]
    [InlineData("- -", null)]          // dashes separated by a space
    [InlineData("\u2010", null)]       // hyphen
    [InlineData("\u2212", null)]       // minus sign
    [InlineData("\uFF0D", null)]       // fullwidth hyphen-minus
    [InlineData("A", "A")]
    [InlineData(" 12/3 ", "12/3")]
    [InlineData("12-3", "12-3")]       // a dash inside a value is part of it
    public void A_dash_only_value_is_a_placeholder_not_text(string? input, string? expected)
    {
        Assert.Equal(expected, PlaceholderText.Stated(input));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("-", null)]
    [InlineData("0", null)]
    [InlineData("\u20130", null)]     // "–0"
    [InlineData("0 -", null)]
    [InlineData("0.0", null)]
    [InlineData("7", "7")]
    [InlineData(" 12 ", "12")]
    [InlineData("G", "G")]            // non-numeric text counts as stated
    [InlineData("5 -", "5 -")]        // sent as the report prints it: trimmed, dashes included
    [InlineData("5 - 6", "5 - 6")]
    [InlineData("-1", "-1")]          // a dash attached to the value is part of it (a basement floor)
    [InlineData("B-2", "B-2")]
    public void A_free_text_number_is_stated_unless_blank_dashes_or_zero(string? input, string? expected)
    {
        Assert.Equal(expected, PlaceholderText.StatedUnlessZero(input));
    }
}
