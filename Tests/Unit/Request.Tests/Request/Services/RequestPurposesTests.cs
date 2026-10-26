using Request.Domain.Requests;

namespace Request.Tests.Request.Services;

public class RequestPurposesTests
{
    [Theory]
    [InlineData("06", true)]
    [InlineData("11", true)]
    [InlineData("12", false)] // needs a prior id like the others, but is not a construction inspection
    [InlineData("03", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ConstructionInspection_IsPurposes06And11(string? purpose, bool expected) =>
        Assert.Equal(expected, RequestPurposes.IsConstructionInspection(purpose));
}
