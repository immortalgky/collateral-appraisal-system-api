using Collateral.Collateral.Shared.Features.CreateCollateral;
using Collateral.Collateral.Shared.Features.DeleteCollateral;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Collateral.Integration.Tests;

public class DeleteCollateralTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task DeleteCollateral_ValidDeleteCollateral_CollateralDisappears()
    {
        var url = "/collaterals";
        // Create Collateral first
        var createCollateralResult =
            await TestCaseHelper.TestCreateEndpoint<CreateCollateralResult>(
                "Collateral.Integration.Tests",
                "CreateCollateral_ValidCollateralLand.json",
                _client,
                url
            );

        // Delete the Collateral
        var deleteCollateralResult =
            await TestCaseHelper.TestDeleteEndpoint<DeleteCollateralResult>(
                createCollateralResult.Id,
                _client,
                url
            );
        Assert.True(deleteCollateralResult.IsSuccess);

        // Get the deleted Collateral by Id
        var getCollateralByIdResponse = await _client.GetAsync(
            $"{url}/{createCollateralResult.Id}",
            TestContext.Current.CancellationToken
        );
        var getCollateralByIdException = Record.Exception(
            getCollateralByIdResponse.EnsureSuccessStatusCode
        );
        Assert.NotNull(getCollateralByIdException);
    }
}
