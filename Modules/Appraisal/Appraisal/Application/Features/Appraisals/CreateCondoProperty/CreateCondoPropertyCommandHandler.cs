using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

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
            command.OwnerName,
            command.Description);

        // 3. Create value objects if provided
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

        // 4. Update detail with additional fields
        property.CondoDetail!.Update(
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

        // 5. Save aggregate
        await appraisalRepository.UpdateAsync(appraisal, cancellationToken);

        // 6. Return both IDs
        return new CreateCondoPropertyResult(property.Id, property.CondoDetail.Id);
    }
}
