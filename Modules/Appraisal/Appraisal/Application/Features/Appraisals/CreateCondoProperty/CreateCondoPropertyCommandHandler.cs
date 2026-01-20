namespace Appraisal.Application.Features.Appraisals.CreateCondoProperty;

/// <summary>
/// Handler for creating a condo property with its appraisal detail
/// </summary>
public class CreateCondoPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<CreateCondoPropertyCommand, CreateCondoPropertyResult>
{
    public async Task<CreateCondoPropertyResult> Handle(
        CreateCondoPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Execute domain operation via aggregate
        var property = appraisal.AddCondoProperty(
            command.OwnerName);

        // 3. Create value objects if provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null || command.LandOffice is not null)
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province,
                command.LandOffice);

        // 4. Update detail with additional fields
        property.CondoDetail!.Update(
            command.PropertyName,
            command.CondoName,
            command.BuildingNumber,
            command.ModelName,
            command.BuiltOnTitleNumber,
            command.CondoRegistrationNumber,
            command.RoomNumber,
            command.FloorNumber,
            command.UsableArea,
            coordinates,
            address,
            command.OwnerName,
            command.IsOwnerVerified,
            command.BuildingConditionType,
            command.HasObligation,
            command.ObligationDetails,
            command.IsDocumentValidated,
            command.LocationType,
            command.Street,
            command.Soi,
            command.DistanceFromMainRoad,
            command.AccessRoadWidth,
            command.RightOfWay,
            command.RoadSurfaceType,
            command.RoadSurfaceTypeOther,
            command.PublicUtilityType,
            command.PublicUtilityTypeOther,
            command.DecorationType,
            command.DecorationTypeOther,
            command.BuildingAge,
            command.ConstructionYear,
            command.NumberOfFloors,
            command.BuildingFormType,
            command.ConstructionMaterialType,
            command.RoomLayoutType,
            command.RoomLayoutTypeOther,
            command.LocationViewType,
            command.GroundFloorMaterialType,
            command.GroundFloorMaterialTypeOther,
            command.UpperFloorMaterialType,
            command.UpperFloorMaterialTypeOther,
            command.BathroomFloorMaterialType,
            command.BathroomFloorMaterialTypeOther,
            command.RoofType,
            command.RoofTypeOther,
            command.TotalBuildingArea,
            command.IsExpropriated,
            command.ExpropriationRemark,
            command.IsInExpropriationLine,
            command.ExpropriationLineRemark,
            command.RoyalDecree,
            command.IsForestBoundary,
            command.ForestBoundaryRemark,
            command.FacilityType,
            command.FacilityTypeOther,
            command.EnvironmentType,
            command.BuildingInsurancePrice,
            command.SellingPrice,
            command.ForcedSalePrice,
            command.Remark);

        // 5. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 6. Return both IDs
        return new CreateCondoPropertyResult(property.Id, property.CondoDetail.Id);
    }
}