using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Exceptions;
using Shared.CQRS;

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

        // 3. Validate property type
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
            BuiltOnTitleNo: detail.BuiltOnTitleNo,
            CondoRegisNo: detail.CondoRegisNo,
            RoomNo: detail.RoomNo,
            FloorNo: detail.FloorNo,
            UsableArea: detail.UsableArea,
            Latitude: detail.Coordinates?.Latitude,
            Longitude: detail.Coordinates?.Longitude,
            SubDistrict: detail.Address?.SubDistrict,
            District: detail.Address?.District,
            Province: detail.Address?.Province,
            LandOffice: detail.Address?.LandOffice,
            Owner: detail.OwnerName,
            VerifiableOwner: detail.IsOwnerVerified,
            CondoCondition: detail.BuildingCondition,
            IsObligation: detail.HasObligation,
            Obligation: detail.ObligationDetails,
            DocValidate: detail.DocValidate,
            CondoLocation: detail.CondoLocation,
            Street: detail.Street,
            Soi: detail.Soi,
            Distance: detail.DistanceFromMainRoad,
            RoadWidth: detail.AccessRoadWidth,
            RightOfWay: detail.RightOfWay,
            RoadSurface: detail.RoadSurfaceType,
            PublicUtility: detail.PublicUtility,
            PublicUtilityOther: detail.PublicUtilityOther,
            Decoration: detail.Decoration,
            DecorationOther: detail.DecorationOther,
            BuildingYear: detail.BuildingYear,
            NumberOfFloors: detail.NumberOfFloors,
            BuildingForm: detail.BuildingForm,
            ConstMaterial: detail.ConstMaterial,
            RoomLayout: detail.RoomLayout,
            RoomLayoutOther: detail.RoomLayoutOther,
            LocationView: detail.LocationView,
            GroundFloorMaterial: detail.GroundFloorMaterial,
            GroundFloorMaterialOther: detail.GroundFloorMaterialOther,
            UpperFloorMaterial: detail.UpperFloorMaterial,
            UpperFloorMaterialOther: detail.UpperFloorMaterialOther,
            BathroomFloorMaterial: detail.BathroomFloorMaterial,
            BathroomFloorMaterialOther: detail.BathroomFloorMaterialOther,
            Roof: detail.Roof,
            RoofOther: detail.RoofOther,
            TotalAreaInSqM: detail.TotalBuildingArea,
            IsExpropriate: detail.IsExpropriated,
            IsExpropriateRemark: detail.ExpropriationRemark,
            InLineExpropriate: detail.IsInExpropriationLine,
            InLineExpropriateRemark: detail.ExpropriationLineRemark,
            RoyalDecree: detail.RoyalDecree,
            IsForestBoundary: detail.IsForestBoundary,
            IsForestBoundaryRemark: detail.ForestBoundaryRemark,
            CondoFacility: detail.CondoFacility,
            CondoFacilityOther: detail.CondoFacilityOther,
            Environment: detail.Environment,
            BuildingInsurancePrice: detail.BuildingInsurancePrice,
            SellingPrice: detail.SellingPrice,
            ForceSellingPrice: detail.ForcedSalePrice,
            Remark: detail.Remark);
    }
}
