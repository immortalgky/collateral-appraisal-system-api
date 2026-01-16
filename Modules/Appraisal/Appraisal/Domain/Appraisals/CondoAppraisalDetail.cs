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
    public string? BuiltOnTitleNumber { get; private set; }
    public string? CondoRegistrationNumber { get; private set; }
    public string? RoomNumber { get; private set; }
    public int? FloorNumber { get; private set; }
    public decimal? UsableArea { get; private set; }

    // GPS Coordinates (Value Object)
    public GpsCoordinate? Coordinates { get; private set; }

    // Administrative Address (Value Object)
    public AdministrativeAddress? Address { get; private set; }

    // Owner
    public string OwnerName { get; private set; } = null!;
    public bool IsOwnerVerified { get; private set; }
    public string? BuildingConditionType { get; private set; }
    public bool HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }
    public bool IsDocumentValidated { get; private set; }

    // Location Details
    public string? LocationType { get; private set; }
    public string? Street { get; private set; }
    public string? Soi { get; private set; }
    public decimal? DistanceFromMainRoad { get; private set; }
    public decimal? AccessRoadWidth { get; private set; }
    public string? RightOfWay { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public List<string>? PublicUtilityType { get; private set; }
    public string? PublicUtilityTypeOther { get; private set; }

    // Building Info
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }
    public int? ConstructionYear { get; private set; }
    public int? NumberOfFloors { get; private set; }
    public string? BuildingFormType { get; private set; }
    public string? ConstructionMaterialType { get; private set; }

    // Layout & Materials
    public string? RoomLayoutType { get; private set; }
    public string? RoomLayoutTypeOther { get; private set; }
    public List<string>? LocationViewType { get; private set; }
    public string? GroundFloorMaterialType { get; private set; }
    public string? GroundFloorMaterialTypeOther { get; private set; }
    public string? UpperFloorMaterialType { get; private set; }
    public string? UpperFloorMaterialTypeOther { get; private set; }
    public string? BathroomFloorMaterialType { get; private set; }
    public string? BathroomFloorMaterialTypeOther { get; private set; }
    public string? RoofType { get; private set; }
    public string? RoofTypeOther { get; private set; }

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
    public List<string>? FacilityType { get; private set; }
    public string? FacilityTypeOther { get; private set; }
    public List<string>? EnvironmentType { get; private set; }

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
        string? condoRegistrationNo = null,
        string? roomNo = null,
        int? floorNo = null,
        decimal? usableArea = null,
        // Value Objects
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        string? buildingConditionType = null,
        bool? hasObligation = null,
        string? obligationDetails = null,
        bool? isDocumentValidated = null,
        // Location Details
        string? locationType = null,
        string? street = null,
        string? soi = null,
        decimal? distanceFromMainRoad = null,
        decimal? accessRoadWidth = null,
        string? rightOfWay = null,
        string? roadSurfaceType = null,
        List<string>? publicUtility = null,
        string? publicUtilityOther = null,
        // Building Info
        string? decorationType = null,
        string? decorationTypeOther = null,
        int? constructionYear = null,
        int? numberOfFloors = null,
        string? buildingForm = null,
        string? constructionMaterialType = null,
        // Layout & Materials
        string? roomLayoutType = null,
        string? roomLayoutTypeOther = null,
        List<string>? locationView = null,
        string? groundFloorMaterial = null,
        string? groundFloorMaterialOther = null,
        string? upperFloorMaterial = null,
        string? upperFloorMaterialOther = null,
        string? bathroomFloorMaterial = null,
        string? bathroomFloorMaterialOther = null,
        string? roofType = null,
        string? roofTypeOther = null,
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
        List<string>? facilityType = null,
        string? facilityTypeOther = null,
        List<string>? environmentType = null,
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
        if (builtOnTitleNo is not null) BuiltOnTitleNumber = builtOnTitleNo;
        if (condoRegistrationNo is not null) CondoRegistrationNumber = condoRegistrationNo;
        if (roomNo is not null) RoomNumber = roomNo;
        if (floorNo.HasValue) FloorNumber = floorNo.Value;
        if (usableArea.HasValue) UsableArea = usableArea.Value;

        // Value Objects
        if (coordinates is not null) Coordinates = coordinates;
        if (address is not null) Address = address;

        // Owner
        if (ownerName is not null) OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;
        if (buildingConditionType is not null) BuildingConditionType = buildingConditionType;
        if (hasObligation.HasValue) HasObligation = hasObligation.Value;
        if (obligationDetails is not null) ObligationDetails = obligationDetails;
        if (isDocumentValidated.HasValue) IsDocumentValidated = isDocumentValidated.Value;

        // Location Details
        if (locationType is not null) LocationType = locationType;
        if (street is not null) Street = street;
        if (soi is not null) Soi = soi;
        if (distanceFromMainRoad.HasValue) DistanceFromMainRoad = distanceFromMainRoad.Value;
        if (accessRoadWidth.HasValue) AccessRoadWidth = accessRoadWidth.Value;
        if (rightOfWay is not null) RightOfWay = rightOfWay;
        if (roadSurfaceType is not null) RoadSurfaceType = roadSurfaceType;
        if (publicUtility is not null) PublicUtilityType = publicUtility;
        if (publicUtilityOther is not null) PublicUtilityTypeOther = publicUtilityOther;

        // Building Info
        if (decorationType is not null) DecorationType = decorationType;
        if (decorationTypeOther is not null) DecorationTypeOther = decorationTypeOther;
        if (constructionYear.HasValue) ConstructionYear = constructionYear.Value;
        if (numberOfFloors.HasValue) NumberOfFloors = numberOfFloors.Value;
        if (buildingForm is not null) BuildingFormType = buildingForm;
        if (constructionMaterialType is not null) ConstructionMaterialType = constructionMaterialType;

        // Layout & Materials
        if (roomLayoutType is not null) RoomLayoutType = roomLayoutType;
        if (roomLayoutTypeOther is not null) RoomLayoutTypeOther = roomLayoutTypeOther;
        if (locationView is not null) LocationViewType = locationView;
        if (groundFloorMaterial is not null) GroundFloorMaterialType = groundFloorMaterial;
        if (groundFloorMaterialOther is not null) GroundFloorMaterialTypeOther = groundFloorMaterialOther;
        if (upperFloorMaterial is not null) UpperFloorMaterialType = upperFloorMaterial;
        if (upperFloorMaterialOther is not null) UpperFloorMaterialTypeOther = upperFloorMaterialOther;
        if (bathroomFloorMaterial is not null) BathroomFloorMaterialType = bathroomFloorMaterial;
        if (bathroomFloorMaterialOther is not null) BathroomFloorMaterialTypeOther = bathroomFloorMaterialOther;
        if (roofType is not null) RoofType = roofType;
        if (roofTypeOther is not null) RoofTypeOther = roofTypeOther;

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
        if (facilityType is not null) FacilityType = facilityType;
        if (facilityTypeOther is not null) FacilityTypeOther = facilityTypeOther;
        if (environmentType is not null) EnvironmentType = environmentType;

        // Pricing
        if (buildingInsurancePrice.HasValue) BuildingInsurancePrice = buildingInsurancePrice.Value;
        if (sellingPrice.HasValue) SellingPrice = sellingPrice.Value;
        if (forcedSalePrice.HasValue) ForcedSalePrice = forcedSalePrice.Value;

        // Other
        if (remark is not null) Remark = remark;
    }
}