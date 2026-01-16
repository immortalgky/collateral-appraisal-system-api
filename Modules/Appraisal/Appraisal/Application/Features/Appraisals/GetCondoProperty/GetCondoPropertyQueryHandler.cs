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
            BuildingNo: detail.BuildingNumber,
            ModelName: detail.ModelName,
            BuiltOnTitleNo: detail.BuiltOnTitleNumber,
            CondoRegistrationNo: detail.CondoRegistrationNumber,
            RoomNo: detail.RoomNumber,
            FloorNo: detail.FloorNumber,
            UsableArea: detail.UsableArea,
            Latitude: detail.Coordinates?.Latitude,
            Longitude: detail.Coordinates?.Longitude,
            SubDistrict: detail.Address?.SubDistrict,
            District: detail.Address?.District,
            Province: detail.Address?.Province,
            LandOffice: detail.Address?.LandOffice,
            Owner: detail.OwnerName,
            VerifiableOwner: detail.IsOwnerVerified,
            BuildingConditionType: detail.BuildingConditionType,
            IsObligation: detail.HasObligation,
            Obligation: detail.ObligationDetails,
            IsDocumentValidated: detail.IsDocumentValidated,
            LocationType: detail.LocationType,
            Street: detail.Street,
            Soi: detail.Soi,
            Distance: detail.DistanceFromMainRoad,
            RoadWidth: detail.AccessRoadWidth,
            RightOfWay: detail.RightOfWay,
            RoadSurface: detail.RoadSurfaceType,
            PublicUtility: detail.PublicUtilityType,
            PublicUtilityOther: detail.PublicUtilityTypeOther,
            DecorationType: detail.DecorationType,
            DecorationTypeOther: detail.DecorationTypeOther,
            ConstructionYear: detail.ConstructionYear,
            NumberOfFloors: detail.NumberOfFloors,
            BuildingForm: detail.BuildingFormType,
            ConstructionMaterialType: detail.ConstructionMaterialType,
            RoomLayoutType: detail.RoomLayoutType,
            RoomLayoutTypeOther: detail.RoomLayoutTypeOther,
            LocationView: detail.LocationViewType,
            GroundFloorMaterial: detail.GroundFloorMaterialType,
            GroundFloorMaterialOther: detail.GroundFloorMaterialTypeOther,
            UpperFloorMaterial: detail.UpperFloorMaterialType,
            UpperFloorMaterialOther: detail.UpperFloorMaterialTypeOther,
            BathroomFloorMaterial: detail.BathroomFloorMaterialType,
            BathroomFloorMaterialOther: detail.BathroomFloorMaterialTypeOther,
            RoofType: detail.RoofType,
            RoofTypeOther: detail.RoofTypeOther,
            TotalAreaInSqM: detail.TotalBuildingArea,
            IsExpropriate: detail.IsExpropriated,
            IsExpropriateRemark: detail.ExpropriationRemark,
            InLineExpropriate: detail.IsInExpropriationLine,
            InLineExpropriateRemark: detail.ExpropriationLineRemark,
            RoyalDecree: detail.RoyalDecree,
            IsForestBoundary: detail.IsForestBoundary,
            IsForestBoundaryRemark: detail.ForestBoundaryRemark,
            FacilityType: detail.FacilityType,
            FacilityTypeOther: detail.FacilityTypeOther,
            EnvironmentType: detail.EnvironmentType,
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForceSellingPrice: detail.ForcedSalePrice,
            Remark: detail.Remark);
    }
}