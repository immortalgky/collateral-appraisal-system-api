using System.Text.Json;
using Collateral.Collateral.Shared.Features.CreateCollateral;
using Collateral.Collateral.Shared.Features.GetCollateralById;
using Integration.Fixtures;
using Integration.Helpers;

namespace Integration.Collateral.Integration.Tests;

public class CreateCollateralTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private readonly string _folderName = "Collateral.Integration.Tests";

    [Fact]
    public async Task CreateCollateral_ValidCollateralLand_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralLand.json";
        await TestCreateCollateral(fileName);
    }

    [Fact]
    public async Task CreateCollateral_ValidCollateralBuilding_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralBuilding.json";
        await TestCreateCollateral(fileName);
    }

    [Fact]
    public async Task CreateCollateral_ValidCollateralLandAndBuilding_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralLandAndBuilding.json";
        await TestCreateCollateral(fileName);
    }

    [Fact]
    public async Task CreateCollateral_ValidCollateralCondo_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralCondo.json";
        await TestCreateCollateral(fileName);
    }

    [Fact]
    public async Task CreateCollateral_ValidCollateralMachine_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralMachine.json";
        await TestCreateCollateral(fileName);
    }

    [Fact]
    public async Task CreateCollateral_ValidCollateralVehicle_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralVehicle.json";
        await TestCreateCollateral(fileName);
    }

    [Fact]
    public async Task CreateCollateral_ValidCollateralVessel_NewCollateralAppears()
    {
        var fileName = "CreateCollateral_ValidCollateralVessel.json";
        await TestCreateCollateral(fileName);
    }

    private async Task TestCreateCollateral(string fileName)
    {
        // Create new collateral
        var createCollateralResult =
            await TestCaseHelper.TestCreateEndpoint<CreateCollateralResult>(
                _folderName,
                fileName,
                _client,
                "/collaterals"
            );
        var createCollateralJson = await JsonHelper.JsonFileToJson(_folderName, fileName);

        // Get the Collateral by Id
        var getCollateralByIdResult =
            await TestCaseHelper.TestGetByIdEndpoint<GetCollateralByIdResult>(
                _client,
                $"/collaterals/{createCollateralResult.Id}"
            );
        Assert.Equivalent(getCollateralByIdResult.Id, createCollateralResult.Id);

        // Compare the result with the original one
        var createCollateralRequest = JsonSerializer.Deserialize<CreateCollateralRequest>(
            createCollateralJson,
            JsonHelper.Options
        );
        Assert.Equivalent(createCollateralRequest!.CollatType, getCollateralByIdResult.CollatType);
    }
}
