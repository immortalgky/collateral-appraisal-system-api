namespace Appraisal.Application.Features.Appraisals.GetCondoProperty;

/// <summary>
/// Handler for getting a condo property with its detail
/// </summary>
public class GetCondoPropertyQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetCondoPropertyQuery, GetCondoPropertyResult>
{
    public async Task<GetCondoPropertyResult> Handle(
        GetCondoPropertyQuery query,
        CancellationToken cancellationToken)
    {
        // 1. Load aggregate root with properties
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            query.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(query.AppraisalId);

        // 2. Find the property
        var property = appraisal.GetProperty(query.PropertyId)
                       ?? throw new PropertyNotFoundException(query.PropertyId);

        // 3. Validate a property type
        if (property.PropertyType != PropertyType.Condo)
            throw new InvalidOperationException($"Property {query.PropertyId} is not a condo property");

        // 4. Get the condo detail
        var detail = property.CondoDetail
                     ?? throw new InvalidOperationException($"Condo detail not found for property {query.PropertyId}");

        // 5. Map to result
        return new GetCondoPropertyResult(
            PropertyId: property.Id,
            AppraisalId: property.AppraisalId,
            SequenceNumber: property.SequenceNumber,
            PropertyType: property.PropertyType.ToString(),
            Description: property.Description,
            DetailId: detail.Id,
            PropertyName: detail.PropertyName,
            CondoName: detail.CondoName,
            BuildingNumber: detail.BuildingNumber,
            ModelName: detail.ModelName,
            BuiltOnTitleNumber: detail.BuiltOnTitleNumber,
            CondoRegistrationNumber: detail.CondoRegistrationNumber,
            RoomNumber: detail.RoomNumber,
            FloorNumber: detail.FloorNumber,
            UsableArea: detail.UsableArea,
            Latitude: detail.Coordinates?.Latitude,
            Longitude: detail.Coordinates?.Longitude,
            SubDistrict: detail.Address?.SubDistrict,
            District: detail.Address?.District,
            Province: detail.Address?.Province,
            LandOffice: detail.Address?.LandOffice,
            OwnerName: detail.OwnerName,
            IsOwnerVerified: detail.IsOwnerVerified,
            BuildingConditionType: detail.BuildingConditionType,
            HasObligation: detail.HasObligation,
            ObligationDetails: detail.ObligationDetails,
            IsDocumentValidated: detail.IsDocumentValidated,
            LocationType: detail.LocationType,
            Street: detail.Street,
            Soi: detail.Soi,
            DistanceFromMainRoad: detail.DistanceFromMainRoad,
            AccessRoadWidth: detail.AccessRoadWidth,
            RightOfWay: detail.RightOfWay,
            RoadSurfaceType: detail.RoadSurfaceType,
            RoadSurfaceTypeOther: detail.RoadSurfaceTypeOther,
            PublicUtilityType: detail.PublicUtilityType,
            PublicUtilityTypeOther: detail.PublicUtilityTypeOther,
            DecorationType: detail.DecorationType,
            DecorationTypeOther: detail.DecorationTypeOther,
            BuildingAge: detail.BuildingAge,
            NumberOfFloors: detail.NumberOfFloors,
            BuildingFormType: detail.BuildingFormType,
            ConstructionMaterialType: detail.ConstructionMaterialType,
            RoomLayoutType: detail.RoomLayoutType,
            RoomLayoutTypeOther: detail.RoomLayoutTypeOther,
            LocationViewType: detail.LocationViewType,
            GroundFloorMaterialType: detail.GroundFloorMaterialType,
            GroundFloorMaterialTypeOther: detail.GroundFloorMaterialTypeOther,
            UpperFloorMaterialType: detail.UpperFloorMaterialType,
            UpperFloorMaterialTypeOther: detail.UpperFloorMaterialTypeOther,
            BathroomFloorMaterialType: detail.BathroomFloorMaterialType,
            BathroomFloorMaterialTypeOther: detail.BathroomFloorMaterialTypeOther,
            RoofType: detail.RoofType,
            RoofTypeOther: detail.RoofTypeOther,
            TotalBuildingArea: detail.TotalBuildingArea,
            IsExpropriated: detail.IsExpropriated,
            ExpropriationRemark: detail.ExpropriationRemark,
            IsInExpropriationLine: detail.IsInExpropriationLine,
            ExpropriationLineRemark: detail.ExpropriationLineRemark,
            RoyalDecree: detail.RoyalDecree,
            IsForestBoundary: detail.IsForestBoundary,
            ForestBoundaryRemark: detail.ForestBoundaryRemark,
            FacilityType: detail.FacilityType,
            FacilityTypeOther: detail.FacilityTypeOther,
            EnvironmentType: detail.EnvironmentType,
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForceSellingPrice: detail.ForcedSalePrice,
            Remark: detail.Remark);
    }
}