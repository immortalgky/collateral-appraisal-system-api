namespace Appraisal.Application.Features.Appraisals.UpdateCondoProperty;

/// <summary>
/// Handler for updating a condo property detail
/// </summary>
public class UpdateCondoPropertyCommandHandler(
    IAppraisalRepository appraisalRepository
) : ICommandHandler<UpdateCondoPropertyCommand>
{
    public async Task<MediatR.Unit> Handle(
        UpdateCondoPropertyCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
            command.AppraisalId, cancellationToken)
            ?? throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(command.PropertyId)
            ?? throw new PropertyNotFoundException(command.PropertyId);

        // 3. Validate a property type
        if (property.PropertyType != PropertyType.Condo)
            throw new InvalidOperationException($"Property {command.PropertyId} is not a condo property");

        // 4. Get the condo detail
        var detail = property.CondoDetail
            ?? throw new InvalidOperationException($"Condo detail not found for property {command.PropertyId}");

        // 5. Create value objects if coordinates or address provided
        GpsCoordinate? coordinates = null;
        if (command.Latitude.HasValue && command.Longitude.HasValue)
        {
            coordinates = GpsCoordinate.Create(command.Latitude.Value, command.Longitude.Value);
        }

        AdministrativeAddress? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null || command.LandOffice is not null)
        {
            address = AdministrativeAddress.Create(
                command.SubDistrict,
                command.District,
                command.Province,
                command.LandOffice);
        }

        // 6. Update via domain method
        detail.Update(
            propertyName: command.PropertyName,
            condoName: command.CondoName,
            buildingNumber: command.BuildingNumber,
            modelName: command.ModelName,
            builtOnTitleNumber: command.BuiltOnTitleNumber,
            condoRegistrationNumber: command.CondoRegistrationNumber,
            roomNumber: command.RoomNumber,
            floorNumber: command.FloorNumber,
            usableArea: command.UsableArea,
            coordinates: coordinates,
            address: address,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            buildingConditionType: command.BuildingConditionType,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            isDocumentValidated: command.IsDocumentValidated,
            locationType: command.LocationType,
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            accessRoadWidth: command.AccessRoadWidth,
            rightOfWay: command.RightOfWay,
            roadSurfaceType: command.RoadSurfaceType,
            publicUtilityType: command.PublicUtilityType,
            publicUtilityTypeOther: command.PublicUtilityTypeOther,
            decorationType: command.DecorationType,
            decorationTypeOther: command.DecorationTypeOther,
            buildingAge: command.BuildingAge,
            constructionYear: command.ConstructionYear,
            numberOfFloors: command.NumberOfFloors,
            buildingFormType: command.BuildingFormType,
            constructionMaterialType: command.ConstructionMaterialType,
            roomLayoutType: command.RoomLayoutType,
            roomLayoutTypeOther: command.RoomLayoutTypeOther,
            locationViewType: command.LocationViewType,
            groundFloorMaterialType: command.GroundFloorMaterialType,
            groundFloorMaterialTypeOther: command.GroundFloorMaterialTypeOther,
            upperFloorMaterialType: command.UpperFloorMaterialType,
            upperFloorMaterialTypeOther: command.UpperFloorMaterialTypeOther,
            bathroomFloorMaterialType: command.BathroomFloorMaterialType,
            bathroomFloorMaterialTypeOther: command.BathroomFloorMaterialTypeOther,
            roofType: command.RoofType,
            roofTypeOther: command.RoofTypeOther,
            totalBuildingArea: command.TotalBuildingArea,
            isExpropriated: command.IsExpropriated,
            expropriationRemark: command.ExpropriationRemark,
            isInExpropriationLine: command.IsInExpropriationLine,
            expropriationLineRemark: command.ExpropriationLineRemark,
            royalDecree: command.RoyalDecree,
            isForestBoundary: command.IsForestBoundary,
            forestBoundaryRemark: command.ForestBoundaryRemark,
            facilityType: command.FacilityType,
            facilityTypeOther: command.FacilityTypeOther,
            environmentType: command.EnvironmentType,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.Remark);

        // 7. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
