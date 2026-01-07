namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Combined table for land+building properties. Contains all fields from both Land and Building,
/// with shared fields (like owner) appearing only once.
/// 1:1 relationship with AppraisalProperty (PropertyType = LandAndBuilding)
/// </summary>
public class LandAndBuildingAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // =====================================================
    // PROPERTY IDENTIFICATION
    // =====================================================
    public string? PropertyName { get; private set; }
    public string? LandDescription { get; private set; }

    // GPS Coordinates (Value Object)
    public GpsCoordinate? Coordinates { get; private set; }

    // Administrative Address (Value Object)
    public AdministrativeAddress? Address { get; private set; }

    // =====================================================
    // SHARED OWNER FIELDS (appear once)
    // =====================================================
    public string OwnerName { get; private set; } = null!;
    public string OwnershipType { get; private set; } = null!;
    public string? OwnershipDocument { get; private set; }
    public decimal? OwnershipPercentage { get; private set; }
    public bool IsOwnerVerified { get; private set; }
    public bool HasObligation { get; private set; }
    public string? ObligationDetails { get; private set; }
    public string? PropertyUsage { get; private set; }
    public string? OccupancyStatus { get; private set; }

    // =====================================================
    // LAND SECTION - Title Deed Info
    // =====================================================
    public string? TitleDeedType { get; private set; }
    public string? TitleDeedNumber { get; private set; }
    public string? LandNumber { get; private set; }
    public string? SurveyPageNumber { get; private set; }

    // Land Area (Value Object)
    public LandArea? Area { get; private set; }

    // =====================================================
    // LAND SECTION - Document Verification
    // =====================================================
    public string? LandLocationVerification { get; private set; }
    public string? LandCheckMethod { get; private set; }
    public string? LandCheckMethodOther { get; private set; }

    // =====================================================
    // LAND SECTION - Location Details
    // =====================================================
    public string? Street { get; private set; }
    public string? Soi { get; private set; }
    public decimal? DistanceFromMainRoad { get; private set; }
    public string? Village { get; private set; }
    public string? AddressLocation { get; private set; }

    // =====================================================
    // LAND SECTION - Land Characteristics
    // =====================================================
    public string? LandShape { get; private set; }
    public string? UrbanPlanningType { get; private set; }
    public string? PlotLocation { get; private set; }
    public string? PlotLocationOther { get; private set; }
    public string? LandFillStatus { get; private set; }
    public string? LandFillStatusOther { get; private set; }
    public decimal? LandFillPercent { get; private set; }
    public string? TerrainType { get; private set; }
    public string? SoilCondition { get; private set; }
    public string? SoilLevel { get; private set; }
    public string? FloodRisk { get; private set; }
    public string? LandUseZoning { get; private set; }
    public string? LandUseZoningOther { get; private set; }

    // =====================================================
    // LAND SECTION - Road Access
    // =====================================================
    public string? AccessRoadType { get; private set; }
    public decimal? AccessRoadWidth { get; private set; }
    public string? RightOfWay { get; private set; }
    public decimal? RoadFrontage { get; private set; }
    public int? NumberOfSidesFacingRoad { get; private set; }
    public string? RoadPassInFrontOfLand { get; private set; }
    public string? LandAccessibility { get; private set; }
    public string? LandAccessibilityDescription { get; private set; }
    public string? RoadSurfaceType { get; private set; }
    public string? RoadSurfaceTypeOther { get; private set; }

    // =====================================================
    // LAND SECTION - Utilities & Infrastructure
    // =====================================================
    public bool? ElectricityAvailable { get; private set; }
    public decimal? ElectricityDistance { get; private set; }
    public bool? WaterSupplyAvailable { get; private set; }
    public bool? SewerageAvailable { get; private set; }
    public string? PublicUtilities { get; private set; }
    public string? PublicUtilitiesOther { get; private set; }
    public string? LandEntranceExit { get; private set; }
    public string? LandEntranceExitOther { get; private set; }
    public string? TransportationAccess { get; private set; }
    public string? TransportationAccessOther { get; private set; }
    public string? PropertyAnticipation { get; private set; }

    // =====================================================
    // LAND SECTION - Legal Restrictions
    // =====================================================
    public bool IsExpropriated { get; private set; }
    public string? ExpropriationRemark { get; private set; }
    public bool IsInExpropriationLine { get; private set; }
    public string? ExpropriationLineRemark { get; private set; }
    public string? RoyalDecree { get; private set; }
    public bool IsEncroached { get; private set; }
    public string? EncroachmentRemark { get; private set; }
    public decimal? EncroachmentArea { get; private set; }
    public bool IsLandlocked { get; private set; }
    public string? LandlockedRemark { get; private set; }
    public bool IsForestBoundary { get; private set; }
    public string? ForestBoundaryRemark { get; private set; }
    public string? OtherLegalLimitations { get; private set; }
    public string? EvictionStatus { get; private set; }
    public string? EvictionStatusOther { get; private set; }
    public string? AllocationStatus { get; private set; }

    // =====================================================
    // LAND SECTION - Adjacent Boundaries (N/S/E/W)
    // =====================================================
    public string? NorthAdjacentArea { get; private set; }
    public decimal? NorthBoundaryLength { get; private set; }
    public string? SouthAdjacentArea { get; private set; }
    public decimal? SouthBoundaryLength { get; private set; }
    public string? EastAdjacentArea { get; private set; }
    public decimal? EastBoundaryLength { get; private set; }
    public string? WestAdjacentArea { get; private set; }
    public decimal? WestBoundaryLength { get; private set; }

    // =====================================================
    // LAND SECTION - Other Land Features
    // =====================================================
    public decimal? PondArea { get; private set; }
    public decimal? PondDepth { get; private set; }

    // =====================================================
    // BUILDING SECTION - Identification
    // =====================================================
    public string? BuildingNumber { get; private set; }
    public string? ModelName { get; private set; }
    public string? BuiltOnTitleNumber { get; private set; }
    public string? HouseNumber { get; private set; }

    // =====================================================
    // BUILDING SECTION - Building Info
    // =====================================================
    public string? BuildingType { get; private set; }
    public string? BuildingTypeOther { get; private set; }
    public int? NumberOfBuildings { get; private set; }
    public int? BuildingAge { get; private set; }
    public int? ConstructionYear { get; private set; }
    public string? IsResidentialRemark { get; private set; }

    // =====================================================
    // BUILDING SECTION - Building Status
    // =====================================================
    public string? BuildingCondition { get; private set; }
    public bool IsUnderConstruction { get; private set; }
    public decimal? ConstructionCompletionPercent { get; private set; }
    public DateTime? ConstructionLicenseExpirationDate { get; private set; }
    public bool IsAppraisable { get; private set; } = true;
    public string? MaintenanceStatus { get; private set; }
    public string? RenovationHistory { get; private set; }

    // =====================================================
    // BUILDING SECTION - Building Area
    // =====================================================
    public decimal? TotalBuildingArea { get; private set; }
    public string? BuildingAreaUnit { get; private set; }
    public decimal? UsableArea { get; private set; }

    // =====================================================
    // BUILDING SECTION - Structure
    // =====================================================
    public int? NumberOfFloors { get; private set; }
    public int? NumberOfUnits { get; private set; }
    public int? NumberOfBedrooms { get; private set; }
    public int? NumberOfBathrooms { get; private set; }

    // =====================================================
    // BUILDING SECTION - Construction Style
    // =====================================================
    public string? BuildingMaterial { get; private set; }
    public string? BuildingStyle { get; private set; }
    public bool? IsResidential { get; private set; }
    public string? ConstructionStyleType { get; private set; }
    public string? ConstructionStyleRemark { get; private set; }
    public string? ConstructionType { get; private set; }
    public string? ConstructionTypeOther { get; private set; }

    // =====================================================
    // BUILDING SECTION - Structure Components
    // =====================================================
    public string? StructureType { get; private set; }
    public string? StructureTypeOther { get; private set; }
    public string? FoundationType { get; private set; }
    public string? RoofFrameType { get; private set; }
    public string? RoofFrameTypeOther { get; private set; }
    public string? RoofType { get; private set; }
    public string? RoofTypeOther { get; private set; }
    public string? RoofMaterial { get; private set; }
    public string? CeilingType { get; private set; }
    public string? CeilingTypeOther { get; private set; }
    public string? InteriorWallType { get; private set; }
    public string? InteriorWallTypeOther { get; private set; }
    public string? ExteriorWallType { get; private set; }
    public string? ExteriorWallTypeOther { get; private set; }
    public string? WallMaterial { get; private set; }
    public string? FloorMaterial { get; private set; }
    public string? FenceType { get; private set; }
    public string? FenceTypeOther { get; private set; }

    // =====================================================
    // BUILDING SECTION - Decoration
    // =====================================================
    public string? DecorationType { get; private set; }
    public string? DecorationTypeOther { get; private set; }

    // =====================================================
    // BUILDING SECTION - Utilization
    // =====================================================
    public string? UtilizationType { get; private set; }
    public string? OtherPurposeUsage { get; private set; }

    // =====================================================
    // BUILDING SECTION - Permits
    // =====================================================
    public string? BuildingPermitNumber { get; private set; }
    public DateTime? BuildingPermitDate { get; private set; }
    public bool? HasOccupancyPermit { get; private set; }

    // =====================================================
    // BUILDING SECTION - Pricing
    // =====================================================
    public decimal? BuildingInsurancePrice { get; private set; }
    public decimal? SellingPrice { get; private set; }
    public decimal? ForcedSalePrice { get; private set; }

    // =====================================================
    // SHARED - Remarks
    // =====================================================
    public string? LandRemark { get; private set; }
    public string? BuildingRemark { get; private set; }

    private LandAndBuildingAppraisalDetail()
    {
    }

    public static LandAndBuildingAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        string ownershipType,
        Guid createdBy)
    {
        return new LandAndBuildingAppraisalDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName,
            OwnershipType = ownershipType,
            NumberOfBuildings = 1,
            IsAppraisable = true
        };
    }

    public void Update(
        // Property Identification
        string? propertyName = null,
        string? landDescription = null,
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        // Owner Fields
        string? ownerName = null,
        string? ownershipType = null,
        string? ownershipDocument = null,
        decimal? ownershipPercentage = null,
        bool? isOwnerVerified = null,
        bool? hasObligation = null,
        string? obligationDetails = null,
        string? propertyUsage = null,
        string? occupancyStatus = null,
        // Land - Title Deed
        string? titleDeedType = null,
        string? titleDeedNumber = null,
        string? landNumber = null,
        string? surveyPageNumber = null,
        LandArea? area = null,
        // Land - Document Verification
        string? landLocationVerification = null,
        string? landCheckMethod = null,
        string? landCheckMethodOther = null,
        // Land - Location Details
        string? street = null,
        string? soi = null,
        decimal? distanceFromMainRoad = null,
        string? village = null,
        string? addressLocation = null,
        // Land - Characteristics
        string? landShape = null,
        string? urbanPlanningType = null,
        string? plotLocation = null,
        string? plotLocationOther = null,
        string? landFillStatus = null,
        string? landFillStatusOther = null,
        decimal? landFillPercent = null,
        string? terrainType = null,
        string? soilCondition = null,
        string? soilLevel = null,
        string? floodRisk = null,
        string? landUseZoning = null,
        string? landUseZoningOther = null,
        // Land - Road Access
        string? accessRoadType = null,
        decimal? accessRoadWidth = null,
        string? rightOfWay = null,
        decimal? roadFrontage = null,
        int? numberOfSidesFacingRoad = null,
        string? roadPassInFrontOfLand = null,
        string? landAccessibility = null,
        string? landAccessibilityDescription = null,
        string? roadSurfaceType = null,
        string? roadSurfaceTypeOther = null,
        // Land - Utilities
        bool? electricityAvailable = null,
        decimal? electricityDistance = null,
        bool? waterSupplyAvailable = null,
        bool? sewerageAvailable = null,
        string? publicUtilities = null,
        string? publicUtilitiesOther = null,
        string? landEntranceExit = null,
        string? landEntranceExitOther = null,
        string? transportationAccess = null,
        string? transportationAccessOther = null,
        string? propertyAnticipation = null,
        // Land - Legal
        bool? isExpropriated = null,
        string? expropriationRemark = null,
        bool? isInExpropriationLine = null,
        string? expropriationLineRemark = null,
        string? royalDecree = null,
        bool? isEncroached = null,
        string? encroachmentRemark = null,
        decimal? encroachmentArea = null,
        bool? isLandlocked = null,
        string? landlockedRemark = null,
        bool? isForestBoundary = null,
        string? forestBoundaryRemark = null,
        string? otherLegalLimitations = null,
        string? evictionStatus = null,
        string? evictionStatusOther = null,
        string? allocationStatus = null,
        // Land - Boundaries
        string? northAdjacentArea = null,
        decimal? northBoundaryLength = null,
        string? southAdjacentArea = null,
        decimal? southBoundaryLength = null,
        string? eastAdjacentArea = null,
        decimal? eastBoundaryLength = null,
        string? westAdjacentArea = null,
        decimal? westBoundaryLength = null,
        // Land - Other
        decimal? pondArea = null,
        decimal? pondDepth = null,
        // Building - Identification
        string? buildingNumber = null,
        string? modelName = null,
        string? builtOnTitleNumber = null,
        string? houseNumber = null,
        // Building - Info
        string? buildingType = null,
        string? buildingTypeOther = null,
        int? numberOfBuildings = null,
        int? buildingAge = null,
        int? constructionYear = null,
        string? isResidentialRemark = null,
        // Building - Status
        string? buildingCondition = null,
        bool? isUnderConstruction = null,
        decimal? constructionCompletionPercent = null,
        DateTime? constructionLicenseExpirationDate = null,
        bool? isAppraisable = null,
        string? maintenanceStatus = null,
        string? renovationHistory = null,
        // Building - Area
        decimal? totalBuildingArea = null,
        string? buildingAreaUnit = null,
        decimal? usableArea = null,
        // Building - Structure
        int? numberOfFloors = null,
        int? numberOfUnits = null,
        int? numberOfBedrooms = null,
        int? numberOfBathrooms = null,
        // Building - Style
        string? buildingMaterial = null,
        string? buildingStyle = null,
        bool? isResidential = null,
        string? constructionStyleType = null,
        string? constructionStyleRemark = null,
        string? constructionType = null,
        string? constructionTypeOther = null,
        // Building - Components
        string? structureType = null,
        string? structureTypeOther = null,
        string? foundationType = null,
        string? roofFrameType = null,
        string? roofFrameTypeOther = null,
        string? roofType = null,
        string? roofTypeOther = null,
        string? roofMaterial = null,
        string? ceilingType = null,
        string? ceilingTypeOther = null,
        string? interiorWallType = null,
        string? interiorWallTypeOther = null,
        string? exteriorWallType = null,
        string? exteriorWallTypeOther = null,
        string? wallMaterial = null,
        string? floorMaterial = null,
        string? fenceType = null,
        string? fenceTypeOther = null,
        // Building - Decoration
        string? decorationType = null,
        string? decorationTypeOther = null,
        // Building - Utilization
        string? utilizationType = null,
        string? otherPurposeUsage = null,
        // Building - Permits
        string? buildingPermitNumber = null,
        DateTime? buildingPermitDate = null,
        bool? hasOccupancyPermit = null,
        // Building - Pricing
        decimal? buildingInsurancePrice = null,
        decimal? sellingPrice = null,
        decimal? forcedSalePrice = null,
        // Remarks
        string? landRemark = null,
        string? buildingRemark = null)
    {
        // Property Identification
        if (propertyName is not null) PropertyName = propertyName;
        if (landDescription is not null) LandDescription = landDescription;
        if (coordinates is not null) Coordinates = coordinates;
        if (address is not null) Address = address;

        // Owner Fields
        if (ownerName is not null) OwnerName = ownerName;
        if (ownershipType is not null) OwnershipType = ownershipType;
        if (ownershipDocument is not null) OwnershipDocument = ownershipDocument;
        if (ownershipPercentage.HasValue) OwnershipPercentage = ownershipPercentage.Value;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;
        if (hasObligation.HasValue) HasObligation = hasObligation.Value;
        if (obligationDetails is not null) ObligationDetails = obligationDetails;
        if (propertyUsage is not null) PropertyUsage = propertyUsage;
        if (occupancyStatus is not null) OccupancyStatus = occupancyStatus;

        // Land - Title Deed
        if (titleDeedType is not null) TitleDeedType = titleDeedType;
        if (titleDeedNumber is not null) TitleDeedNumber = titleDeedNumber;
        if (landNumber is not null) LandNumber = landNumber;
        if (surveyPageNumber is not null) SurveyPageNumber = surveyPageNumber;
        if (area is not null) Area = area;

        // Land - Document Verification
        if (landLocationVerification is not null) LandLocationVerification = landLocationVerification;
        if (landCheckMethod is not null) LandCheckMethod = landCheckMethod;
        if (landCheckMethodOther is not null) LandCheckMethodOther = landCheckMethodOther;

        // Land - Location Details
        if (street is not null) Street = street;
        if (soi is not null) Soi = soi;
        if (distanceFromMainRoad.HasValue) DistanceFromMainRoad = distanceFromMainRoad.Value;
        if (village is not null) Village = village;
        if (addressLocation is not null) AddressLocation = addressLocation;

        // Land - Characteristics
        if (landShape is not null) LandShape = landShape;
        if (urbanPlanningType is not null) UrbanPlanningType = urbanPlanningType;
        if (plotLocation is not null) PlotLocation = plotLocation;
        if (plotLocationOther is not null) PlotLocationOther = plotLocationOther;
        if (landFillStatus is not null) LandFillStatus = landFillStatus;
        if (landFillStatusOther is not null) LandFillStatusOther = landFillStatusOther;
        if (landFillPercent.HasValue) LandFillPercent = landFillPercent.Value;
        if (terrainType is not null) TerrainType = terrainType;
        if (soilCondition is not null) SoilCondition = soilCondition;
        if (soilLevel is not null) SoilLevel = soilLevel;
        if (floodRisk is not null) FloodRisk = floodRisk;
        if (landUseZoning is not null) LandUseZoning = landUseZoning;
        if (landUseZoningOther is not null) LandUseZoningOther = landUseZoningOther;

        // Land - Road Access
        if (accessRoadType is not null) AccessRoadType = accessRoadType;
        if (accessRoadWidth.HasValue) AccessRoadWidth = accessRoadWidth.Value;
        if (rightOfWay is not null) RightOfWay = rightOfWay;
        if (roadFrontage.HasValue) RoadFrontage = roadFrontage.Value;
        if (numberOfSidesFacingRoad.HasValue) NumberOfSidesFacingRoad = numberOfSidesFacingRoad.Value;
        if (roadPassInFrontOfLand is not null) RoadPassInFrontOfLand = roadPassInFrontOfLand;
        if (landAccessibility is not null) LandAccessibility = landAccessibility;
        if (landAccessibilityDescription is not null) LandAccessibilityDescription = landAccessibilityDescription;
        if (roadSurfaceType is not null) RoadSurfaceType = roadSurfaceType;
        if (roadSurfaceTypeOther is not null) RoadSurfaceTypeOther = roadSurfaceTypeOther;

        // Land - Utilities
        if (electricityAvailable.HasValue) ElectricityAvailable = electricityAvailable.Value;
        if (electricityDistance.HasValue) ElectricityDistance = electricityDistance.Value;
        if (waterSupplyAvailable.HasValue) WaterSupplyAvailable = waterSupplyAvailable.Value;
        if (sewerageAvailable.HasValue) SewerageAvailable = sewerageAvailable.Value;
        if (publicUtilities is not null) PublicUtilities = publicUtilities;
        if (publicUtilitiesOther is not null) PublicUtilitiesOther = publicUtilitiesOther;
        if (landEntranceExit is not null) LandEntranceExit = landEntranceExit;
        if (landEntranceExitOther is not null) LandEntranceExitOther = landEntranceExitOther;
        if (transportationAccess is not null) TransportationAccess = transportationAccess;
        if (transportationAccessOther is not null) TransportationAccessOther = transportationAccessOther;
        if (propertyAnticipation is not null) PropertyAnticipation = propertyAnticipation;

        // Land - Legal
        if (isExpropriated.HasValue) IsExpropriated = isExpropriated.Value;
        if (expropriationRemark is not null) ExpropriationRemark = expropriationRemark;
        if (isInExpropriationLine.HasValue) IsInExpropriationLine = isInExpropriationLine.Value;
        if (expropriationLineRemark is not null) ExpropriationLineRemark = expropriationLineRemark;
        if (royalDecree is not null) RoyalDecree = royalDecree;
        if (isEncroached.HasValue) IsEncroached = isEncroached.Value;
        if (encroachmentRemark is not null) EncroachmentRemark = encroachmentRemark;
        if (encroachmentArea.HasValue) EncroachmentArea = encroachmentArea.Value;
        if (isLandlocked.HasValue) IsLandlocked = isLandlocked.Value;
        if (landlockedRemark is not null) LandlockedRemark = landlockedRemark;
        if (isForestBoundary.HasValue) IsForestBoundary = isForestBoundary.Value;
        if (forestBoundaryRemark is not null) ForestBoundaryRemark = forestBoundaryRemark;
        if (otherLegalLimitations is not null) OtherLegalLimitations = otherLegalLimitations;
        if (evictionStatus is not null) EvictionStatus = evictionStatus;
        if (evictionStatusOther is not null) EvictionStatusOther = evictionStatusOther;
        if (allocationStatus is not null) AllocationStatus = allocationStatus;

        // Land - Boundaries
        if (northAdjacentArea is not null) NorthAdjacentArea = northAdjacentArea;
        if (northBoundaryLength.HasValue) NorthBoundaryLength = northBoundaryLength.Value;
        if (southAdjacentArea is not null) SouthAdjacentArea = southAdjacentArea;
        if (southBoundaryLength.HasValue) SouthBoundaryLength = southBoundaryLength.Value;
        if (eastAdjacentArea is not null) EastAdjacentArea = eastAdjacentArea;
        if (eastBoundaryLength.HasValue) EastBoundaryLength = eastBoundaryLength.Value;
        if (westAdjacentArea is not null) WestAdjacentArea = westAdjacentArea;
        if (westBoundaryLength.HasValue) WestBoundaryLength = westBoundaryLength.Value;

        // Land - Other
        if (pondArea.HasValue) PondArea = pondArea.Value;
        if (pondDepth.HasValue) PondDepth = pondDepth.Value;

        // Building - Identification
        if (buildingNumber is not null) BuildingNumber = buildingNumber;
        if (modelName is not null) ModelName = modelName;
        if (builtOnTitleNumber is not null) BuiltOnTitleNumber = builtOnTitleNumber;
        if (houseNumber is not null) HouseNumber = houseNumber;

        // Building - Info
        if (buildingType is not null) BuildingType = buildingType;
        if (buildingTypeOther is not null) BuildingTypeOther = buildingTypeOther;
        if (numberOfBuildings.HasValue) NumberOfBuildings = numberOfBuildings.Value;
        if (buildingAge.HasValue) BuildingAge = buildingAge.Value;
        if (constructionYear.HasValue) ConstructionYear = constructionYear.Value;
        if (isResidentialRemark is not null) IsResidentialRemark = isResidentialRemark;

        // Building - Status
        if (buildingCondition is not null) BuildingCondition = buildingCondition;
        if (isUnderConstruction.HasValue) IsUnderConstruction = isUnderConstruction.Value;
        if (constructionCompletionPercent.HasValue) ConstructionCompletionPercent = constructionCompletionPercent.Value;
        if (constructionLicenseExpirationDate.HasValue) ConstructionLicenseExpirationDate = constructionLicenseExpirationDate.Value;
        if (isAppraisable.HasValue) IsAppraisable = isAppraisable.Value;
        if (maintenanceStatus is not null) MaintenanceStatus = maintenanceStatus;
        if (renovationHistory is not null) RenovationHistory = renovationHistory;

        // Building - Area
        if (totalBuildingArea.HasValue) TotalBuildingArea = totalBuildingArea.Value;
        if (buildingAreaUnit is not null) BuildingAreaUnit = buildingAreaUnit;
        if (usableArea.HasValue) UsableArea = usableArea.Value;

        // Building - Structure
        if (numberOfFloors.HasValue) NumberOfFloors = numberOfFloors.Value;
        if (numberOfUnits.HasValue) NumberOfUnits = numberOfUnits.Value;
        if (numberOfBedrooms.HasValue) NumberOfBedrooms = numberOfBedrooms.Value;
        if (numberOfBathrooms.HasValue) NumberOfBathrooms = numberOfBathrooms.Value;

        // Building - Style
        if (buildingMaterial is not null) BuildingMaterial = buildingMaterial;
        if (buildingStyle is not null) BuildingStyle = buildingStyle;
        if (isResidential.HasValue) IsResidential = isResidential.Value;
        if (constructionStyleType is not null) ConstructionStyleType = constructionStyleType;
        if (constructionStyleRemark is not null) ConstructionStyleRemark = constructionStyleRemark;
        if (constructionType is not null) ConstructionType = constructionType;
        if (constructionTypeOther is not null) ConstructionTypeOther = constructionTypeOther;

        // Building - Components
        if (structureType is not null) StructureType = structureType;
        if (structureTypeOther is not null) StructureTypeOther = structureTypeOther;
        if (foundationType is not null) FoundationType = foundationType;
        if (roofFrameType is not null) RoofFrameType = roofFrameType;
        if (roofFrameTypeOther is not null) RoofFrameTypeOther = roofFrameTypeOther;
        if (roofType is not null) RoofType = roofType;
        if (roofTypeOther is not null) RoofTypeOther = roofTypeOther;
        if (roofMaterial is not null) RoofMaterial = roofMaterial;
        if (ceilingType is not null) CeilingType = ceilingType;
        if (ceilingTypeOther is not null) CeilingTypeOther = ceilingTypeOther;
        if (interiorWallType is not null) InteriorWallType = interiorWallType;
        if (interiorWallTypeOther is not null) InteriorWallTypeOther = interiorWallTypeOther;
        if (exteriorWallType is not null) ExteriorWallType = exteriorWallType;
        if (exteriorWallTypeOther is not null) ExteriorWallTypeOther = exteriorWallTypeOther;
        if (wallMaterial is not null) WallMaterial = wallMaterial;
        if (floorMaterial is not null) FloorMaterial = floorMaterial;
        if (fenceType is not null) FenceType = fenceType;
        if (fenceTypeOther is not null) FenceTypeOther = fenceTypeOther;

        // Building - Decoration
        if (decorationType is not null) DecorationType = decorationType;
        if (decorationTypeOther is not null) DecorationTypeOther = decorationTypeOther;

        // Building - Utilization
        if (utilizationType is not null) UtilizationType = utilizationType;
        if (otherPurposeUsage is not null) OtherPurposeUsage = otherPurposeUsage;

        // Building - Permits
        if (buildingPermitNumber is not null) BuildingPermitNumber = buildingPermitNumber;
        if (buildingPermitDate.HasValue) BuildingPermitDate = buildingPermitDate.Value;
        if (hasOccupancyPermit.HasValue) HasOccupancyPermit = hasOccupancyPermit.Value;

        // Building - Pricing
        if (buildingInsurancePrice.HasValue) BuildingInsurancePrice = buildingInsurancePrice.Value;
        if (sellingPrice.HasValue) SellingPrice = sellingPrice.Value;
        if (forcedSalePrice.HasValue) ForcedSalePrice = forcedSalePrice.Value;

        // Remarks
        if (landRemark is not null) LandRemark = landRemark;
        if (buildingRemark is not null) BuildingRemark = buildingRemark;
    }
}
