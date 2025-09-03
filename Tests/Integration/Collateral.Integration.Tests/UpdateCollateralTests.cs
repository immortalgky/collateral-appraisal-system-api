using System.Text.Json;
using Collateral.Collateral.Shared.Features.CreateCollateral;
using Collateral.Collateral.Shared.Features.GetCollateralById;
using Collateral.Collateral.Shared.Features.UpdateCollateral;
using Integration.Fixtures;
using Integration.Helpers;
using Shared.Dtos;

namespace Integration.Collateral.Integration.Tests;

public class UpdateCollateralTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private readonly string _folderName = "Collateral.Integration.Tests";
    private readonly string _url = "/collaterals";

    [Fact]
    public async Task UpdateCollateral_ValidUpdateLand_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateLand_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateLand.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralLand?.CollateralLocation.SubDistrict,
            getCollateralByIdResult.CollateralLand?.CollateralLocation.SubDistrict
        );
        Assert.Equal(
            updateCollateralRequest?.LandTitles?.Count,
            getCollateralByIdResult?.LandTitles?.Count
        );
    }

    [Fact]
    public async Task UpdateCollateral_ValidUpdateBuilding_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateBuilding_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateBuilding.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralBuilding?.BuildingNo,
            getCollateralByIdResult.CollateralBuilding?.BuildingNo
        );
    }

    [Fact]
    public async Task UpdateCollateral_ValidUpdateCondo_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateCondo_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateCondo.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralCondo?.CondoName,
            getCollateralByIdResult.CollateralCondo?.CondoName
        );
    }

    [Fact]
    public async Task UpdateCollateral_ValidUpdateLandAndBuilding_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateLandAndBuilding_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateLandAndBuilding.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralLand?.Coordinate.Latitude,
            getCollateralByIdResult.CollateralLand?.Coordinate.Latitude
        );
    }

    [Fact]
    public async Task UpdateCollateral_ValidUpdateMachine_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateMachine_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateMachine.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralMachine?.CollateralMachineProperty.Name,
            getCollateralByIdResult.CollateralMachine?.CollateralMachineProperty.Name
        );
    }

    [Fact]
    public async Task UpdateCollateral_ValidUpdateVehicle_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateVehicle_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateVehicle.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralVehicle?.CollateralVehicleProperty.Name,
            getCollateralByIdResult.CollateralVehicle?.CollateralVehicleProperty.Name
        );
    }

    [Fact]
    public async Task UpdateCollateral_ValidUpdateVessel_CollateralIsUpdated()
    {
        var createFileName = "UpdateCollateral_ValidUpdateVessel_Create.json";
        var updateFileName = "UpdateCollateral_ValidUpdateVessel.json";

        var (getCollateralByIdResult, updateCollateralRequest) = await TestUpdateCollateral(
            createFileName,
            updateFileName
        );

        Assert.Equal(
            updateCollateralRequest?.CollateralVessel?.CollateralVesselProperty.Name,
            getCollateralByIdResult.CollateralVessel?.CollateralVesselProperty.Name
        );
    }

    [Fact]
    public async Task UpdateCollateral_LandTitleUpdatingRequest_LandTitleIsUpdated()
    {
        var createFileName = "UpdateCollateral_LandTitleUpdatingRequest_Create.json";
        var updateFileName = "UpdateCollateral_LandTitleUpdatingRequest.json";

        // Create new collateral
        var createCollateralResult =
            await TestCaseHelper.TestCreateEndpoint<CreateCollateralResult>(
                _folderName,
                createFileName,
                _client,
                _url
            );

        // Get the land title ID and use it in the update request
        var getCollateralByIdResult =
            await TestCaseHelper.TestGetByIdEndpoint<GetCollateralByIdResult>(
                createCollateralResult.Id,
                _client,
                _url
            );
        var landTitleId = getCollateralByIdResult.LandTitles![0].Id;
        LandTitleDto? landTitleDto = null; // Will be assigned in closure

        // Change ID in update collateral request json to be current land title ID
        string jsonTransformFunc(string j)
        {
            var updateCollateralRequest = JsonSerializer.Deserialize<UpdateCollateralRequest>(
                j,
                JsonHelper.Options
            );
            var oldLandTitle = updateCollateralRequest!.LandTitles![0];
            landTitleDto = new LandTitleDto(
                landTitleId,
                oldLandTitle.SeqNo,
                oldLandTitle.LandTitleDocumentDetail,
                oldLandTitle.LandTitleArea,
                oldLandTitle.DocumentType,
                oldLandTitle.Rawang,
                oldLandTitle.AerialPhotoNo,
                oldLandTitle.BoundaryMarker,
                oldLandTitle.BoundaryMarkerOther,
                oldLandTitle.DocValidate,
                oldLandTitle.PricePerSquareWa,
                oldLandTitle.GovernmentPrice
            );
            updateCollateralRequest!.LandTitles![0] = landTitleDto;

            var newUpdateCollateralRequest = JsonSerializer.Serialize(
                updateCollateralRequest,
                JsonHelper.Options
            );

            return newUpdateCollateralRequest;
        }

        var updateCollateralResult =
            await TestCaseHelper.TestUpdateEndpoint<UpdateCollateralResult>(
                _folderName,
                updateFileName,
                createCollateralResult.Id,
                _client,
                _url,
                jsonTransformFunc
            );

        Assert.True(updateCollateralResult.IsSuccess);

        // Get the collateral again to check the updated land title
        var secondGetCollateralByIdResult =
            await TestCaseHelper.TestGetByIdEndpoint<GetCollateralByIdResult>(
                createCollateralResult.Id,
                _client,
                _url
            );

        Assert.Equal(landTitleId, secondGetCollateralByIdResult?.LandTitles?[0].Id);
        Assert.Equal(landTitleDto?.SeqNo, secondGetCollateralByIdResult?.LandTitles?[0].SeqNo);
    }

    private async Task<(GetCollateralByIdResult, UpdateCollateralRequest)> TestUpdateCollateral(
        string createFileName,
        string updateFileName
    )
    {
        // Create new collateral
        var createCollateralResult =
            await TestCaseHelper.TestCreateEndpoint<CreateCollateralResult>(
                _folderName,
                createFileName,
                _client,
                _url
            );
        var updateCollateralResult =
            await TestCaseHelper.TestUpdateEndpoint<UpdateCollateralResult>(
                _folderName,
                updateFileName,
                createCollateralResult.Id,
                _client,
                _url
            );
        Assert.True(updateCollateralResult.IsSuccess);

        var getCollateralByIdResult =
            await TestCaseHelper.TestGetByIdEndpoint<GetCollateralByIdResult>(
                createCollateralResult.Id,
                _client,
                _url
            );

        var updateCollateralJson = await JsonHelper.JsonFileToJson(_folderName, updateFileName);
        var updateCollateralRequest = JsonSerializer.Deserialize<UpdateCollateralRequest>(
            updateCollateralJson,
            JsonHelper.Options
        );

        return (getCollateralByIdResult, updateCollateralRequest!);
    }
}
