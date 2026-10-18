using Request.Contracts.Requests.Dtos;
using Request.Domain.Requests;
using Request.Extensions;

namespace Request.Tests.Request.Requests.Models;

public class RequestPropertyTests
{
    [Theory]
    [InlineData("L")]
    [InlineData("LSL")]
    [InlineData("MAC")]
    [InlineData("VEH")]
    [InlineData("VES")]
    public void NoBuildingTypes_PassWithoutBuildingType(string propertyType)
    {
        var property = RequestProperty.Create(propertyType, null, null, 1);
        Assert.Null(Record.Exception(() => property.Validate("RBG")));
    }

    [Theory]
    [InlineData("L")]
    [InlineData("LSL")]
    [InlineData("MAC")]
    [InlineData("VEH")]
    [InlineData("VES")]
    public void NoBuildingTypes_DropBuildingTypeAndOther(string propertyType)
    {
        var property = RequestProperty.Create(propertyType, "01", "stale", 1);
        Assert.Null(property.BuildingType);
        Assert.Null(property.BuildingTypeOther);
    }

    [Theory]
    [InlineData("LB")]
    [InlineData("B")]
    [InlineData("U")]
    [InlineData("LSU")]
    [InlineData("LS")]
    [InlineData("LSB")]
    public void BuildingTypes_ThrowWithoutBuildingType(string propertyType)
    {
        var property = RequestProperty.Create(propertyType, null, null, 1);
        Assert.ThrowsAny<ArgumentException>(() => property.Validate("RBG"));
    }

    [Fact]
    public void NoBuildingType_WithZeroSellingPrice_ThrowsUnlessIbg()
    {
        var property = RequestProperty.Create("L", null, null, 0);
        Assert.ThrowsAny<ArgumentException>(() => property.Validate("RBG"));
        Assert.Null(Record.Exception(() => property.Validate("IBG")));
    }

    [Fact]
    public void LowercaseCode_IsNotCanonical_StillRequiresBuildingType()
    {
        var property = RequestProperty.Create("l", null, null, 1);
        Assert.ThrowsAny<ArgumentException>(() => property.Validate("RBG"));
    }

    [Fact]
    public void ToDomain_LandDtoWithBuildingType_EndsUpWithNullBuildingType()
    {
        var property = new RequestPropertyDto("L", "01", "stale", 1).ToDomain();
        Assert.Null(property.BuildingType);
        Assert.Null(property.BuildingTypeOther);
    }

    [Fact]
    public void BuildingTypes_KeepBuildingType()
    {
        var property = RequestProperty.Create("LB", "01", "other", 1);
        Assert.Equal("01", property.BuildingType);
        Assert.Equal("other", property.BuildingTypeOther);
    }
}
