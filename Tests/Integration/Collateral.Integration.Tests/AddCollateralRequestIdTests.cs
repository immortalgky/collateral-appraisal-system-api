using Collateral.Collateral.Shared.Features.CreateCollateral;
using Collateral.Collateral.Shared.Features.GetCollateralById;
using Collateral.Collateral.Shared.Features.UpdateCollateralEngagement;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Collateral.Integration.Tests;

public class UpdateCollateralEngagementTests(IntegrationTestFixture fixture)
    : IntegrationTestBase(fixture)
{
    private readonly string _folderName = "Collateral.Integration.Tests";

    [Fact]
    public async Task UpdateCollateralEngagement_ValidRequest_RequestIdIsAddedToCollateral()
    {
        var createFileName = "UpdateCollateralEngagement_ValidRequest_Create.json";
        var addCollateralFileName = "UpdateCollateralEngagement_ValidRequest.json";

        // Create new collateral
        var createCollateralResult =
            await TestCaseHelper.TestCreateEndpoint<CreateCollateralResult>(
                _folderName,
                createFileName,
                _client,
                "/collaterals"
            );

        var UpdateCollateralEngagementResult =
            await TestCaseHelper.TestUpdateEndpoint<UpdateCollateralEngagementResult>(
                _folderName,
                addCollateralFileName,
                _client,
                $"/collaterals/{createCollateralResult.Id}/engagements"
            );

        Assert.True(UpdateCollateralEngagementResult.IsSuccess);

        // Check that the request ID is added to the collateral
        var getCollateralByIdResult =
            await TestCaseHelper.TestGetByIdEndpoint<GetCollateralByIdResult>(
                _client,
                $"/collaterals/{createCollateralResult.Id}"
            );

        Assert.Contains(getCollateralByIdResult.CollateralEngagements, r => r.ReqId == 2);
    }
}
