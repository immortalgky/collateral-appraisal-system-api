using Collateral.Collateral.Shared.Features.AddCollateralRequestId;
using Collateral.Collateral.Shared.Features.CreateCollateral;
using Collateral.Collateral.Shared.Features.GetCollateralById;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Collateral.Integration.Tests;

public class AddCollateralRequestIdTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    private readonly string _folderName = "Collateral.Integration.Tests";

    [Fact]
    public async Task AddCollateralRequestId_ValidRequest_RequestIdIsAddedToCollateral()
    {
        var createFileName = "AddCollateralRequestId_ValidRequest_Create.json";
        var addCollateralFileName = "AddCollateralRequestId_ValidRequest.json";

        // Create new collateral
        var createCollateralResult =
            await TestCaseHelper.TestCreateEndpoint<CreateCollateralResult>(
                _folderName,
                createFileName,
                _client,
                "/collaterals"
            );

        var addCollateralRequestIdResult =
            await TestCaseHelper.TestCreateEndpoint<AddCollateralRequestIdResult>(
                _folderName,
                addCollateralFileName,
                _client,
                $"/collaterals/{createCollateralResult.Id}/req-ids"
            );

        Assert.True(addCollateralRequestIdResult.IsSuccess);

        // Check that the request ID is added to the collateral
        var getCollateralByIdResult =
            await TestCaseHelper.TestGetByIdEndpoint<GetCollateralByIdResult>(
                createCollateralResult.Id,
                _client,
                "/collaterals"
            );

        Assert.Contains(getCollateralByIdResult.ReqIds, r => r == 2);
    }
}
