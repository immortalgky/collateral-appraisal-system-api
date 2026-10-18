using Appraisal.Application.Features.Appraisals.GetInProgressAppraisalsByCollateral;
using NSubstitute;
using Shared.Data;

namespace Appraisal.Tests.Application.Features;

public class GetInProgressAppraisalsByCollateralTests
{
    private static GetInProgressAppraisalsByCollateralQuery Query(
        string type,
        string? province = null, string? district = null, string? subDistrict = null, string? titleNumber = null,
        string? condoReg = null, string? building = null, string? floor = null, string? unit = null,
        string? machineReg = null) =>
        new(type, province, district, subDistrict, titleNumber, condoReg, building, floor, unit, machineReg);

    [Theory]
    [InlineData("L", true)]
    [InlineData("LB", true)]
    [InlineData("U", true)]
    [InlineData("MAC", true)]
    [InlineData("LSL", false)]
    [InlineData("l", false)]
    [InlineData("", false)]
    public void Only_the_lookup_types_this_endpoint_supports_pass_validation(string type, bool valid)
    {
        var result = new GetInProgressAppraisalsByCollateralQueryValidator().Validate(Query(type));

        Assert.Equal(valid, result.IsValid);
    }

    [Theory]
    [MemberData(nameof(IncompleteKeys))]
    public async Task An_incomplete_key_returns_an_empty_list_without_touching_the_database(
        GetInProgressAppraisalsByCollateralQuery query)
    {
        var factory = Substitute.For<ISqlConnectionFactory>();
        var handler = new GetInProgressAppraisalsByCollateralQueryHandler(factory);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.Empty(result);
        factory.DidNotReceiveWithAnyArgs().GetOpenConnection();
    }

    public static TheoryData<GetInProgressAppraisalsByCollateralQuery> IncompleteKeys() => new()
    {
        Query("L", province: "10", district: "1001", subDistrict: "100101"),              // no title number
        Query("LB", province: "10", district: "1001", titleNumber: "123"),                // no sub-district
        Query("U", condoReg: "R1", building: "A", floor: "3", unit: "301", province: "10", district: "1001"),
        Query("MAC", machineReg: "  "),
    };
}
