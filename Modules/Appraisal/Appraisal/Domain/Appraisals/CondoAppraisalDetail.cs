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
    public string? OwnerName { get; private set; }
    public bool? IsOwnerVerified { get; private set; }
    public string? BuildingConditionType { get; private set; }
    public bool? HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }
    public bool? IsDocumentValidated { get; private set; }

    // Location Details
    public string? LocationType { get; private set; }
    public string? Street { get; private set; }
    public string? Soi { get; private set; }
    public decimal? DistanceFromMainRoad { get; private set; }
    public decimal? AccessRoadWidth { get; private set; }
    public short? RightOfWay { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public string? RoadSurfaceTypeOther { get; private set; }
    public List<string>? PublicUtilityType { get; private set; }
    public string? PublicUtilityTypeOther { get; private set; }

    // Building Info
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public decimal? NumberOfFloors { get; private set; }
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
    public bool? IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool? IsInExpropriationLine { get; private set; }
    public string? ExpropriationLineRemark { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool? IsForestBoundary { get; private set; }
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
        // For EF Core
    }

    public static CondoAppraisalDetail Create(Guid appraisalPropertyId)
    {
        return new CondoAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId
        };
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
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
        short? rightOfWay = null,
        string? roadSurfaceType = null,
        string? roadSurfaceTypeOther = null,
        List<string>? publicUtility = null,
        string? publicUtilityOther = null,
        // Building Info
        string? decorationType = null,
        string? decorationTypeOther = null,
        int? buildingAge = null,
        int? constructionYear = null,
        decimal? numberOfFloors = null,
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
        PropertyName = propertyName;
        CondoName = condoName;
        BuildingNumber = buildingNumber;
        ModelName = modelName;
        BuiltOnTitleNumber = builtOnTitleNo;
        CondoRegistrationNumber = condoRegistrationNo;
        RoomNumber = roomNo;
        FloorNumber = floorNo;
        UsableArea = usableArea;

        // Value Objects
        Coordinates = coordinates;
        Address = address;

        // Owner
        OwnerName = ownerName;
        IsOwnerVerified = isOwnerVerified;
        BuildingConditionType = buildingConditionType;
        HasObligation = hasObligation;
        ObligationDetails = obligationDetails;
        IsDocumentValidated = isDocumentValidated;

        // Location Details
        LocationType = locationType;
        Street = street;
        Soi = soi;
        DistanceFromMainRoad = distanceFromMainRoad;
        AccessRoadWidth = accessRoadWidth;
        RightOfWay = rightOfWay;
        RoadSurfaceType = roadSurfaceType;
        RoadSurfaceTypeOther = roadSurfaceTypeOther;
        PublicUtilityType = publicUtility;
        PublicUtilityTypeOther = publicUtilityOther;

        // Building Info
        DecorationType = decorationType;
        DecorationTypeOther = decorationTypeOther;
        BuildingAge = buildingAge;
        ConstructionYear = constructionYear;
        NumberOfFloors = numberOfFloors;
        BuildingFormType = buildingForm;
        ConstructionMaterialType = constructionMaterialType;

        // Layout & Materials
        RoomLayoutType = roomLayoutType;
        RoomLayoutTypeOther = roomLayoutTypeOther;
        LocationViewType = locationView;
        GroundFloorMaterialType = groundFloorMaterial;
        GroundFloorMaterialTypeOther = groundFloorMaterialOther;
        UpperFloorMaterialType = upperFloorMaterial;
        UpperFloorMaterialTypeOther = upperFloorMaterialOther;
        BathroomFloorMaterialType = bathroomFloorMaterial;
        BathroomFloorMaterialTypeOther = bathroomFloorMaterialOther;
        RoofType = roofType;
        RoofTypeOther = roofTypeOther;

        // Area
        TotalBuildingArea = totalBuildingArea;

        // Legal Restrictions
        IsExpropriated = isExpropriated;
        ExpropriationRemark = expropriationRemark;
        IsInExpropriationLine = isInExpropriationLine;
        ExpropriationLineRemark = expropriationLineRemark;
        RoyalDecree = royalDecree;
        IsForestBoundary = isForestBoundary;
        ForestBoundaryRemark = forestBoundaryRemark;

        // Facilities & Environment
        FacilityType = facilityType;
        FacilityTypeOther = facilityTypeOther;
        EnvironmentType = environmentType;

        // Pricing
        BuildingInsurancePrice = buildingInsurancePrice;
        SellingPrice = sellingPrice;
        ForcedSalePrice = forcedSalePrice;

        // Other
        Remark = remark;
    }
}