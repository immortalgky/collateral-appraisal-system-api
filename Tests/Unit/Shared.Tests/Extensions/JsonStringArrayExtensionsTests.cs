using Shared.Extensions;

namespace Shared.Tests.Extensions;

public class JsonStringArrayExtensionsTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("[1, 2]")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("""["Retail", """)]
    public void ParseJsonStringArray_NullBlankEmptyOrMalformed_ReturnsEmpty(string? json) =>
        Assert.Empty(json.ParseJsonStringArray());

    [Fact]
    public void ParseJsonStringArray_Array_TrimsAndDropsBlankAndNullElements()
    {
        var parsed = """[" Retail ", null, "", "  ", "IBG"]""".ParseJsonStringArray();

        Assert.Equal(["Retail", "IBG"], parsed);
    }
}
