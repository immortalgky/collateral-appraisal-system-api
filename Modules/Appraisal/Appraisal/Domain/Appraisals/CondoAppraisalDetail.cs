namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Condominium property appraisal details including location, materials, and facilities.
/// 1:1 relationship with AppraisalProperty (PropertyType = Condo)
/// </summary>
public class CondoAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Property Identification
    public string? PropertyName { get; private set; }
    public string? CondoName { get; private set; }
    public string? BuildingNumber { get; private set; }
    public string? ModelName { get; private set; }
    public string? BuiltOnTitleNo { get; private set; }
    public string? CondoRegisNo { get; private set; }
    public string? RoomNo { get; private set; }
    public int? FloorNo { get; private set; }
    public decimal? UsableArea { get; private set; }

    // GPS Coordinates (Value Object)
    public GpsCoordinate? Coordinates { get; private set; }

    // Administrative Address (Value Object)
    public AdministrativeAddress? Address { get; private set; }

    // Owner
    public string OwnerName { get; private set; } = null!;
    public bool IsOwnerVerified { get; private set; }
    public string? BuildingCondition { get; private set; }
    public bool HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }
    public bool DocValidate { get; private set; }

    // Location Details
    public string? CondoLocation { get; private set; }
    public string? Street { get; private set; }
    public string? Soi { get; private set; }
    public decimal? DistanceFromMainRoad { get; private set; }
    public decimal? AccessRoadWidth { get; private set; }
    public string? RightOfWay { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public string? PublicUtility { get; private set; }
    public string? PublicUtilityOther { get; private set; }

    // Building Info
    public string? Decoration { get; private set; }
    public string? DecorationOther { get; private set; }
    public int? BuildingYear { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public string? BuildingForm { get; private set; }
    public string? ConstMaterial { get; private set; }

    // Layout & Materials
    public string? RoomLayout { get; private set; }
    public string? RoomLayoutOther { get; private set; }
    public string? LocationView { get; private set; }
    public string? GroundFloorMaterial { get; private set; }
    public string? GroundFloorMaterialOther { get; private set; }
    public string? UpperFloorMaterial { get; private set; }
    public string? UpperFloorMaterialOther { get; private set; }
    public string? BathroomFloorMaterial { get; private set; }
    public string? BathroomFloorMaterialOther { get; private set; }
    public string? Roof { get; private set; }
    public string? RoofOther { get; private set; }

    // Area
    public decimal? TotalBuildingArea { get; private set; }

    // Legal Restrictions
    public bool IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool IsInExpropriationLine { get; private set; }
    public string? ExpropriationLineRemark { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool IsForestBoundary { get; private set; }
    public string? ForestBoundaryRemark { get; private set; }

    // Facilities & Environment
    public string? CondoFacility { get; private set; }
    public string? CondoFacilityOther { get; private set; }
    public string? Environment { get; private set; }

    // Pricing
    public decimal? BuildingInsurancePrice { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public decimal? ForcedSalePrice { get; private set; }

    // Other
    public string? Remark { get; private set; }

    private CondoAppraisalDetail()
    {
    }

    public static CondoAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        Guid createdBy)
    {
        return new CondoAppraisalDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName
        };
    }

    public void Update(
        // Property Identification
        string? propertyName = null,
        string? condoName = null,
        string? buildingNumber = null,
        string? modelName = null,
        string? builtOnTitleNo = null,
        string? condoRegisNo = null,
        string? roomNo = null,
        int? floorNo = null,
        decimal? usableArea = null,
        // Value Objects
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        string? buildingCondition = null,
        bool? hasObligation = null,
        string? obligationDetails = null,
        bool? docValidate = null,
        // Location Details
        string? condoLocation = null,
        string? street = null,
        string? soi = null,
        decimal? distanceFromMainRoad = null,
        decimal? accessRoadWidth = null,
        string? rightOfWay = null,
        string? roadSurfaceType = null,
        string? publicUtility = null,
        string? publicUtilityOther = null,
        // Building Info
        string? decoration = null,
        string? decorationOther = null,
        int? buildingYear = null,
        int? numberOfFloors = null,
        string? buildingForm = null,
        string? constMaterial = null,
        // Layout & Materials
        string? roomLayout = null,
        string? roomLayoutOther = null,
        string? locationView = null,
        string? groundFloorMaterial = null,
        string? groundFloorMaterialOther = null,
        string? upperFloorMaterial = null,
        string? upperFloorMaterialOther = null,
        string? bathroomFloorMaterial = null,
        string? bathroomFloorMaterialOther = null,
        string? roof = null,
        string? roofOther = null,
        // Area
        decimal? totalBuildingArea = null,
        // Legal Restrictions
        bool? isExpropriated = null,
        string? expropriationRemark = null,
        bool? isInExpropriationLine = null,
        string? expropriationLineRemark = null,
        string? royalDecree = null,
        bool? isForestBoundary = null,
        string? forestBoundaryRemark = null,
        // Facilities & Environment
        string? condoFacility = null,
        string? condoFacilityOther = null,
        string? environment = null,
        // Pricing
        decimal? buildingInsurancePrice = null,
        decimal? sellingPrice = null,
        decimal? forcedSalePrice = null,
        // Other
        string? remark = null)
    {
        // Property Identification
        if (propertyName is not null) PropertyName = propertyName;
        if (condoName is not null) CondoName = condoName;
        if (buildingNumber is not null) BuildingNumber = buildingNumber;
        if (modelName is not null) ModelName = modelName;
        if (builtOnTitleNo is not null) BuiltOnTitleNo = builtOnTitleNo;
        if (condoRegisNo is not null) CondoRegisNo = condoRegisNo;
        if (roomNo is not null) RoomNo = roomNo;
        if (floorNo.HasValue) FloorNo = floorNo.Value;
        if (usableArea.HasValue) UsableArea = usableArea.Value;

        // Value Objects
        if (coordinates is not null) Coordinates = coordinates;
        if (address is not null) Address = address;

        // Owner
        if (ownerName is not null) OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;
        if (buildingCondition is not null) BuildingCondition = buildingCondition;
        if (hasObligation.HasValue) HasObligation = hasObligation.Value;
        if (obligationDetails is not null) ObligationDetails = obligationDetails;
        if (docValidate.HasValue) DocValidate = docValidate.Value;

        // Location Details
        if (condoLocation is not null) CondoLocation = condoLocation;
        if (street is not null) Street = street;
        if (soi is not null) Soi = soi;
        if (distanceFromMainRoad.HasValue) DistanceFromMainRoad = distanceFromMainRoad.Value;
        if (accessRoadWidth.HasValue) AccessRoadWidth = accessRoadWidth.Value;
        if (rightOfWay is not null) RightOfWay = rightOfWay;
        if (roadSurfaceType is not null) RoadSurfaceType = roadSurfaceType;
        if (publicUtility is not null) PublicUtility = publicUtility;
        if (publicUtilityOther is not null) PublicUtilityOther = publicUtilityOther;

        // Building Info
        if (decoration is not null) Decoration = decoration;
        if (decorationOther is not null) DecorationOther = decorationOther;
        if (buildingYear.HasValue) BuildingYear = buildingYear.Value;
        if (numberOfFloors.HasValue) NumberOfFloors = numberOfFloors.Value;
        if (buildingForm is not null) BuildingForm = buildingForm;
        if (constMaterial is not null) ConstMaterial = constMaterial;

        // Layout & Materials
        if (roomLayout is not null) RoomLayout = roomLayout;
        if (roomLayoutOther is not null) RoomLayoutOther = roomLayoutOther;
        if (locationView is not null) LocationView = locationView;
        if (groundFloorMaterial is not null) GroundFloorMaterial = groundFloorMaterial;
        if (groundFloorMaterialOther is not null) GroundFloorMaterialOther = groundFloorMaterialOther;
        if (upperFloorMaterial is not null) UpperFloorMaterial = upperFloorMaterial;
        if (upperFloorMaterialOther is not null) UpperFloorMaterialOther = upperFloorMaterialOther;
        if (bathroomFloorMaterial is not null) BathroomFloorMaterial = bathroomFloorMaterial;
        if (bathroomFloorMaterialOther is not null) BathroomFloorMaterialOther = bathroomFloorMaterialOther;
        if (roof is not null) Roof = roof;
        if (roofOther is not null) RoofOther = roofOther;

        // Area
        if (totalBuildingArea.HasValue) TotalBuildingArea = totalBuildingArea.Value;

        // Legal Restrictions
        if (isExpropriated.HasValue) IsExpropriated = isExpropriated.Value;
        if (expropriationRemark is not null) ExpropriationRemark = expropriationRemark;
        if (isInExpropriationLine.HasValue) IsInExpropriationLine = isInExpropriationLine.Value;
        if (expropriationLineRemark is not null) ExpropriationLineRemark = expropriationLineRemark;
        if (royalDecree is not null) RoyalDecree = royalDecree;
        if (isForestBoundary.HasValue) IsForestBoundary = isForestBoundary.Value;
        if (forestBoundaryRemark is not null) ForestBoundaryRemark = forestBoundaryRemark;

        // Facilities & Environment
        if (condoFacility is not null) CondoFacility = condoFacility;
        if (condoFacilityOther is not null) CondoFacilityOther = condoFacilityOther;
        if (environment is not null) Environment = environment;

        // Pricing
        if (buildingInsurancePrice.HasValue) BuildingInsurancePrice = buildingInsurancePrice.Value;
        if (sellingPrice.HasValue) SellingPrice = sellingPrice.Value;
        if (forcedSalePrice.HasValue) ForcedSalePrice = forcedSalePrice.Value;

        // Other
        if (remark is not null) Remark = remark;
    }
}
