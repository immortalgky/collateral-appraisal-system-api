using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

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

        // 3. Validate property type
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
            builtOnTitleNo: command.BuiltOnTitleNo,
            condoRegisNo: command.CondoRegisNo,
            roomNo: command.RoomNo,
            floorNo: command.FloorNo,
            usableArea: command.UsableArea,
            coordinates: coordinates,
            address: address,
            ownerName: command.OwnerName,
            isOwnerVerified: command.IsOwnerVerified,
            buildingCondition: command.BuildingCondition,
            hasObligation: command.HasObligation,
            obligationDetails: command.ObligationDetails,
            docValidate: command.DocValidate,
            condoLocation: command.CondoLocation,
            street: command.Street,
            soi: command.Soi,
            distanceFromMainRoad: command.DistanceFromMainRoad,
            accessRoadWidth: command.AccessRoadWidth,
            rightOfWay: command.RightOfWay,
            roadSurfaceType: command.RoadSurfaceType,
            publicUtility: command.PublicUtility,
            publicUtilityOther: command.PublicUtilityOther,
            decoration: command.Decoration,
            decorationOther: command.DecorationOther,
            buildingYear: command.BuildingYear,
            numberOfFloors: command.NumberOfFloors,
            buildingForm: command.BuildingForm,
            constMaterial: command.ConstMaterial,
            roomLayout: command.RoomLayout,
            roomLayoutOther: command.RoomLayoutOther,
            locationView: command.LocationView,
            groundFloorMaterial: command.GroundFloorMaterial,
            groundFloorMaterialOther: command.GroundFloorMaterialOther,
            upperFloorMaterial: command.UpperFloorMaterial,
            upperFloorMaterialOther: command.UpperFloorMaterialOther,
            bathroomFloorMaterial: command.BathroomFloorMaterial,
            bathroomFloorMaterialOther: command.BathroomFloorMaterialOther,
            roof: command.Roof,
            roofOther: command.RoofOther,
            totalBuildingArea: command.TotalBuildingArea,
            isExpropriated: command.IsExpropriated,
            expropriationRemark: command.ExpropriationRemark,
            isInExpropriationLine: command.IsInExpropriationLine,
            expropriationLineRemark: command.ExpropriationLineRemark,
            royalDecree: command.RoyalDecree,
            isForestBoundary: command.IsForestBoundary,
            forestBoundaryRemark: command.ForestBoundaryRemark,
            condoFacility: command.CondoFacility,
            condoFacilityOther: command.CondoFacilityOther,
            environment: command.Environment,
            buildingInsurancePrice: command.BuildingInsurancePrice,
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            remark: command.Remark);

        // 7. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        return MediatR.Unit.Value;
    }
}
