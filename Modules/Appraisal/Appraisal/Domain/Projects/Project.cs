using Appraisal.Domain.Projects.Events;
using Appraisal.Domain.Projects.Exceptions;

namespace Appraisal.Domain.Projects;

/// <summary>
/// Aggregate root for a Block-Condo or Block-Village (LandAndBuilding) project.
/// Replaces the dual CondoProject/VillageProject entities previously owned by the Appraisal aggregate.
/// 1:1 with Appraisal via AppraisalId (FK, cascade-delete from Appraisal).
/// ProjectType discriminates between the two project flavours; mutators enforce type guards.
/// </summary>
public class Project : Aggregate<Guid>
{
    // ----- Identity -----
    public Guid AppraisalId { get; private set; }
    public ProjectType ProjectType { get; private set; }

    // ----- Shared Project Info -----
    public string? ProjectName { get; private set; }
    public string? ProjectDescription { get; private set; }
    public string? Developer { get; private set; }
    public DateTime? ProjectSaleLaunchDate { get; private set; }

    // Land Area
    public decimal? LandAreaRai { get; private set; }
    public decimal? LandAreaNgan { get; private set; }
    public decimal? LandAreaWa { get; private set; }

    // Project Details
    public int? UnitForSaleCount { get; private set; }
    public int? NumberOfPhase { get; private set; }
    public string? LandOffice { get; private set; }

    // Location (value objects)
    public GpsCoordinate? Coordinates { get; private set; }
    public AdministrativeAddress? Address { get; private set; }
    public string? Postcode { get; private set; }
    public string? LocationNumber { get; private set; }
    public string? Road { get; private set; }
    public string? Soi { get; private set; }

    // Utilities & Facilities (JSON lists)
    public List<string>? Utilities { get; private set; }
    public string? UtilitiesOther { get; private set; }
    public List<string>? Facilities { get; private set; }
    public string? FacilitiesOther { get; private set; }

    // Other
    public string? Remark { get; private set; }

    // ----- Type-Specific Nullable Fields -----

    /// <summary>Condo only — built-on title deed number.</summary>
    public string? BuiltOnTitleDeedNumber { get; private set; }

    /// <summary>LandAndBuilding only — project license expiration date.</summary>
    public DateTime? LicenseExpirationDate { get; private set; }

    // ----- Private Collections -----

    private readonly List<ProjectTower> _towers = [];
    private readonly List<ProjectModel> _models = [];
    private readonly List<ProjectUnit> _units = [];
    private readonly List<ProjectUnitUpload> _unitUploads = [];

    // ----- Read-Only Exposures -----

    public IReadOnlyList<ProjectTower> Towers => _towers.AsReadOnly();
    public IReadOnlyList<ProjectModel> Models => _models.AsReadOnly();
    public IReadOnlyList<ProjectUnit> Units => _units.AsReadOnly();
    public IReadOnlyList<ProjectUnitUpload> UnitUploads => _unitUploads.AsReadOnly();
    // UnitPrices are NOT a navigation on this aggregate; query via dbContext.ProjectUnitPrices.Where(p => unitIds.Contains(p.ProjectUnitId))

    // ----- 1:1 Children -----
    public ProjectPricingAssumption? PricingAssumption { get; private set; }
    public ProjectLand? Land { get; private set; }

    private Project()
    {
    }

    // =========================================================================
    // Factory
    // =========================================================================

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Project Create(
        Guid appraisalId,
        ProjectType projectType,
        // Project Info
        string? projectName = null,
        string? projectDescription = null,
        string? developer = null,
        DateTime? projectSaleLaunchDate = null,
        // Land Area
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        // Project Details
        int? unitForSaleCount = null,
        int? numberOfPhase = null,
        string? landOffice = null,
        // Location
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        string? postcode = null,
        string? locationNumber = null,
        string? road = null,
        string? soi = null,
        // Utilities & Facilities
        List<string>? utilities = null,
        string? utilitiesOther = null,
        List<string>? facilities = null,
        string? facilitiesOther = null,
        // Other
        string? remark = null,
        // Type-specific
        string? builtOnTitleDeedNumber = null,
        DateTime? licenseExpirationDate = null)
    {
        ValidateSharedFields(landAreaRai, landAreaNgan, landAreaWa, unitForSaleCount, numberOfPhase);
        ValidateTypeSpecificFields(projectType, builtOnTitleDeedNumber, licenseExpirationDate);

        var project = new Project
        {
            Id = Guid.CreateVersion7(),
            AppraisalId = appraisalId,
            ProjectType = projectType,
            ProjectName = projectName,
            ProjectDescription = projectDescription,
            Developer = developer,
            ProjectSaleLaunchDate = projectSaleLaunchDate,
            LandAreaRai = landAreaRai,
            LandAreaNgan = landAreaNgan,
            LandAreaWa = landAreaWa,
            UnitForSaleCount = unitForSaleCount,
            NumberOfPhase = numberOfPhase,
            LandOffice = landOffice,
            Coordinates = coordinates,
            Address = address,
            Postcode = postcode,
            LocationNumber = locationNumber,
            Road = road,
            Soi = soi,
            Utilities = utilities,
            UtilitiesOther = utilitiesOther,
            Facilities = facilities,
            FacilitiesOther = facilitiesOther,
            Remark = remark,
            BuiltOnTitleDeedNumber = projectType == ProjectType.Condo ? builtOnTitleDeedNumber : null,
            LicenseExpirationDate = projectType == ProjectType.LandAndBuilding ? licenseExpirationDate : null
        };

        project.AddDomainEvent(new ProjectCreatedEvent(project));
        return project;
    }

    // =========================================================================
    // Update shared fields
    // =========================================================================

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void Update(
        // Project Info
        string? projectName = null,
        string? projectDescription = null,
        string? developer = null,
        DateTime? projectSaleLaunchDate = null,
        // Land Area
        decimal? landAreaRai = null,
        decimal? landAreaNgan = null,
        decimal? landAreaWa = null,
        // Project Details
        int? unitForSaleCount = null,
        int? numberOfPhase = null,
        string? landOffice = null,
        // Location
        GpsCoordinate? coordinates = null,
        AdministrativeAddress? address = null,
        string? postcode = null,
        string? locationNumber = null,
        string? road = null,
        string? soi = null,
        // Utilities & Facilities
        List<string>? utilities = null,
        string? utilitiesOther = null,
        List<string>? facilities = null,
        string? facilitiesOther = null,
        // Other
        string? remark = null,
        // Type-specific
        string? builtOnTitleDeedNumber = null,
        DateTime? licenseExpirationDate = null)
    {
        ValidateSharedFields(landAreaRai, landAreaNgan, landAreaWa, unitForSaleCount, numberOfPhase);
        ValidateTypeSpecificFields(ProjectType, builtOnTitleDeedNumber, licenseExpirationDate);

        ProjectName = projectName;
        ProjectDescription = projectDescription;
        Developer = developer;
        ProjectSaleLaunchDate = projectSaleLaunchDate;
        LandAreaRai = landAreaRai;
        LandAreaNgan = landAreaNgan;
        LandAreaWa = landAreaWa;
        UnitForSaleCount = unitForSaleCount;
        NumberOfPhase = numberOfPhase;
        LandOffice = landOffice;
        Coordinates = coordinates;
        Address = address;
        Postcode = postcode;
        LocationNumber = locationNumber;
        Road = road;
        Soi = soi;
        Utilities = utilities;
        UtilitiesOther = utilitiesOther;
        Facilities = facilities;
        FacilitiesOther = facilitiesOther;
        Remark = remark;
        BuiltOnTitleDeedNumber = ProjectType == ProjectType.Condo ? builtOnTitleDeedNumber : null;
        LicenseExpirationDate = ProjectType == ProjectType.LandAndBuilding ? licenseExpirationDate : null;
    }

    // =========================================================================
    // Tower management (Condo only)
    // =========================================================================

    public ProjectTower AddTower()
    {
        RequireCondo(nameof(AddTower));
        var tower = ProjectTower.Create(Id);
        _towers.Add(tower);
        return tower;
    }

    public ProjectTower AddTower(string towerName)
    {
        RequireCondo(nameof(AddTower));
        var tower = ProjectTower.Create(Id, towerName);
        _towers.Add(tower);
        return tower;
    }

    public ProjectTower UpdateTower(Guid towerId)
    {
        RequireCondo(nameof(UpdateTower));
        return _towers.FirstOrDefault(t => t.Id == towerId)
               ?? throw new InvalidProjectStateException($"Project tower {towerId} not found");
    }

    public void RemoveTower(Guid towerId)
    {
        RequireCondo(nameof(RemoveTower));
        var tower = _towers.FirstOrDefault(t => t.Id == towerId)
                    ?? throw new InvalidProjectStateException($"Project tower {towerId} not found");
        _towers.Remove(tower);
    }

    // =========================================================================
    // Model management (both types)
    // =========================================================================

    public ProjectModel AddModel()
    {
        var model = ProjectModel.Create(Id);
        _models.Add(model);
        return model;
    }

    public ProjectModel AddModel(string modelName)
    {
        var model = ProjectModel.Create(Id, modelName);
        _models.Add(model);
        return model;
    }

    public ProjectModel UpdateModel(Guid modelId)
    {
        return _models.FirstOrDefault(m => m.Id == modelId)
               ?? throw new InvalidProjectStateException($"Project model {modelId} not found");
    }

    public void RemoveModel(Guid modelId)
    {
        var model = _models.FirstOrDefault(m => m.Id == modelId)
                    ?? throw new InvalidProjectStateException($"Project model {modelId} not found");
        _models.Remove(model);
    }

    // =========================================================================
    // Unit import (branches internally on ProjectType)
    // =========================================================================

    /// <summary>
    /// Imports a batch of project units from a CSV upload without destroying previously-calculated
    /// unit prices. Units are appended; duplicates are detected by UploadBatchId (a second call
    /// with the same batchId throws <see cref="InvalidProjectStateException"/>).
    /// To explicitly replace all units (and lose existing prices), call <see cref="ReplaceUnits"/> instead.
    /// </summary>
    public ProjectUnitUpload ImportUnits(string fileName, Guid? documentId, List<ProjectUnit> units)
    {
        var upload = ProjectUnitUpload.Create(Id, fileName, documentId);
        _unitUploads.Add(upload);

        // Guard against re-importing the same batch
        if (_units.Any(u => u.UploadBatchId == upload.Id))
            throw new InvalidProjectStateException(
                $"Upload batch '{upload.Id}' has already been imported for this project.");

        // Mark previous uploads as unused; mark this one as used
        foreach (var existing in _unitUploads.Where(u => u.IsUsed && u.Id != upload.Id))
            existing.MarkAsUnused();
        upload.MarkAsUsed();

        foreach (var unit in units)
        {
            unit.SetUploadBatchId(upload.Id);
            _units.Add(unit);
        }

        if (ProjectType == ProjectType.Condo)
        {
            AutoCreateCondoTowersAndModels();
            LinkCondoUnitsToTowersAndModels();
        }
        else
        {
            AutoCreateLandAndBuildingModels();
            LinkLandAndBuildingUnitsToModels();
        }

        return upload;
    }

    /// <summary>
    /// Replaces ALL existing units with the new batch, clearing any previously-calculated unit prices
    /// (cascade via FK). Use this only for an explicit "reset units" action.
    /// </summary>
    public ProjectUnitUpload ReplaceUnits(string fileName, Guid? documentId, List<ProjectUnit> units)
    {
        var upload = ProjectUnitUpload.Create(Id, fileName, documentId);
        _unitUploads.Add(upload);

        // Mark previous uploads as unused; mark this one as used
        foreach (var existing in _unitUploads.Where(u => u.IsUsed && u.Id != upload.Id))
            existing.MarkAsUnused();
        upload.MarkAsUsed();

        // Destructive clear — cascade FK will remove associated ProjectUnitPrice rows
        _units.Clear();

        foreach (var unit in units)
        {
            unit.SetUploadBatchId(upload.Id);
            _units.Add(unit);
        }

        if (ProjectType == ProjectType.Condo)
        {
            AutoCreateCondoTowersAndModels();
            LinkCondoUnitsToTowersAndModels();
        }
        else
        {
            AutoCreateLandAndBuildingModels();
            LinkLandAndBuildingUnitsToModels();
        }

        return upload;
    }

    // =========================================================================
    // Pricing assumption (type-aware)
    // =========================================================================

    /// <summary>
    /// Returns the existing PricingAssumption or creates a new one.
    /// Caller must subsequently call UpdateCondo/UpdateLandAndBuilding on the returned entity.
    /// </summary>
    public ProjectPricingAssumption GetOrCreatePricingAssumption()
    {
        if (PricingAssumption is null)
        {
            PricingAssumption = ProjectPricingAssumption.Create(Id);
        }

        return PricingAssumption;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public ProjectPricingAssumption SetCondoPricingAssumption(
        string? locationMethod,
        decimal? cornerAdjustment,
        decimal? edgeAdjustment,
        decimal? poolViewAdjustment,
        decimal? southAdjustment,
        decimal? otherAdjustment,
        int? floorIncrementEveryXFloor,
        decimal? floorIncrementAmount,
        decimal? forceSalePercentage)
    {
        RequireCondo(nameof(SetCondoPricingAssumption));

        if (PricingAssumption is null)
            PricingAssumption = ProjectPricingAssumption.Create(Id);

        PricingAssumption.UpdateCondo(
            locationMethod, cornerAdjustment, edgeAdjustment,
            poolViewAdjustment, southAdjustment, otherAdjustment,
            floorIncrementEveryXFloor, floorIncrementAmount, forceSalePercentage);

        return PricingAssumption;
    }

    public ProjectPricingAssumption SetLandAndBuildingPricingAssumption(
        string? locationMethod,
        decimal? cornerAdjustment,
        decimal? edgeAdjustment,
        decimal? nearGardenAdjustment,
        decimal? otherAdjustment,
        decimal? landIncreaseDecreaseRate,
        decimal? forceSalePercentage)
    {
        RequireLandAndBuilding(nameof(SetLandAndBuildingPricingAssumption));

        if (PricingAssumption is null)
            PricingAssumption = ProjectPricingAssumption.Create(Id);

        PricingAssumption.UpdateLandAndBuilding(
            locationMethod, cornerAdjustment, edgeAdjustment,
            nearGardenAdjustment, otherAdjustment, landIncreaseDecreaseRate, forceSalePercentage);

        return PricingAssumption;
    }

    // =========================================================================
    // Land (LandAndBuilding only)
    // =========================================================================

    public ProjectLand SetLand(ProjectLand land)
    {
        RequireLandAndBuilding(nameof(SetLand));
        Land = land;
        return Land;
    }

    public ProjectLand GetOrCreateLand()
    {
        RequireLandAndBuilding(nameof(GetOrCreateLand));
        if (Land is null)
            Land = ProjectLand.Create(Id);
        return Land;
    }

    // =========================================================================
    // Unit price calculation (pure domain logic; handler upserts the results)
    // =========================================================================

    /// <summary>
    /// Calculates unit prices for all units in this project.
    /// Returns a list of (unitId, updatedPrice) pairs. The handler upserts these against
    /// the database, decoupling the business rules from persistence concerns.
    /// Throws <see cref="InvalidProjectStateException"/> if PricingAssumption is not set.
    /// </summary>
    public IReadOnlyList<ProjectUnitPrice> CalculateUnitPrices(
        IReadOnlyDictionary<Guid, ProjectUnitPrice> existingPriceMap)
    {
        if (PricingAssumption is null)
            throw new InvalidProjectStateException(
                "Pricing assumptions must be set before calculating prices.");

        return ProjectType == ProjectType.Condo
            ? CalculateCondoUnitPrices(existingPriceMap)
            : CalculateLandAndBuildingUnitPrices(existingPriceMap);
    }

    private IReadOnlyList<ProjectUnitPrice> CalculateCondoUnitPrices(
        IReadOnlyDictionary<Guid, ProjectUnitPrice> existingPriceMap)
    {
        var assumption = PricingAssumption!;
        var hasPersistedAssumptions = assumption.ModelAssumptions.Count > 0;

        var modelLookup = hasPersistedAssumptions
            ? assumption.ModelAssumptions
                .Where(ma => ma.ModelType != null)
                .GroupBy(ma => ma.ModelType!)
                .ToDictionary(g => g.Key, g => (
                    StandardPrice: g.First().StandardPrice ?? 0m,
                    CoverageAmount: g.First().CoverageAmount))
            : _models
                .Where(m => m.ModelName != null)
                .GroupBy(m => m.ModelName!)
                .ToDictionary(g => g.Key, g => (
                    StandardPrice: g.First().StandardPrice ?? 0m,
                    CoverageAmount: CoverageByCondition.Lookup(g.First().FireInsuranceCondition)));

        var results = new List<ProjectUnitPrice>();

        foreach (var unit in _units)
        {
            var unitPrice = existingPriceMap.TryGetValue(unit.Id, out var existing)
                ? existing
                : ProjectUnitPrice.Create(unit.Id);

            decimal standardPrice = 0m;
            decimal? coverageAmount = null;
            if (unit.ModelType != null && modelLookup.TryGetValue(unit.ModelType, out var matched))
            {
                standardPrice = matched.StandardPrice;
                coverageAmount = matched.CoverageAmount;
            }

            var adjustPriceLocation = 0m;
            if (unitPrice.IsCorner) adjustPriceLocation += assumption.CornerAdjustment ?? 0m;
            if (unitPrice.IsEdge) adjustPriceLocation += assumption.EdgeAdjustment ?? 0m;
            if (unitPrice.IsPoolView) adjustPriceLocation += assumption.PoolViewAdjustment ?? 0m;
            if (unitPrice.IsSouth) adjustPriceLocation += assumption.SouthAdjustment ?? 0m;
            if (unitPrice.IsOther) adjustPriceLocation += assumption.OtherAdjustment ?? 0m;

            var priceIncrementPerFloor = 0m;
            if (unit.Floor.HasValue
                && assumption.FloorIncrementEveryXFloor.HasValue
                && assumption.FloorIncrementEveryXFloor.Value > 0
                && assumption.FloorIncrementAmount.HasValue)
            {
                var floorGroups = (unit.Floor.Value - 1) / assumption.FloorIncrementEveryXFloor.Value;
                priceIncrementPerFloor = floorGroups * assumption.FloorIncrementAmount.Value;
            }

            var usableArea = unit.UsableArea ?? 0m;
            var totalAppraisalValue = (standardPrice * usableArea) + adjustPriceLocation + priceIncrementPerFloor;
            var totalAppraisalValueRounded = Math.Round(totalAppraisalValue, 0);
            var forceSellingPrice = assumption.ForceSalePercentage.HasValue
                ? Math.Round(totalAppraisalValueRounded * assumption.ForceSalePercentage.Value / 100m, 0)
                : (decimal?)null;

            unitPrice.UpdateCondoCalculatedValues(
                adjustPriceLocation, standardPrice, priceIncrementPerFloor,
                totalAppraisalValue, totalAppraisalValueRounded, forceSellingPrice, coverageAmount);

            results.Add(unitPrice);
        }

        return results.AsReadOnly();
    }

    private IReadOnlyList<ProjectUnitPrice> CalculateLandAndBuildingUnitPrices(
        IReadOnlyDictionary<Guid, ProjectUnitPrice> existingPriceMap)
    {
        var assumption = PricingAssumption!;

        var modelAssumptionMap = assumption.ModelAssumptions
            .Where(ma => ma.ModelType != null)
            .GroupBy(ma => ma.ModelType!)
            .ToDictionary(g => g.Key, g => g.First());

        var projectModelMap = _models
            .Where(m => m.ModelName != null)
            .GroupBy(m => m.ModelName!)
            .ToDictionary(g => g.Key, g => g.First());

        var results = new List<ProjectUnitPrice>();

        foreach (var unit in _units)
        {
            var unitPrice = existingPriceMap.TryGetValue(unit.Id, out var existing)
                ? existing
                : ProjectUnitPrice.Create(unit.Id);

            ProjectModelAssumption? modelAssumption = null;
            if (unit.ModelType != null && modelAssumptionMap.TryGetValue(unit.ModelType, out var matchedAssumption))
                modelAssumption = matchedAssumption;

            var standardLandArea = 0m;
            if (unit.ModelType != null && projectModelMap.TryGetValue(unit.ModelType, out var projectModel))
                standardLandArea = projectModel.StandardLandArea ?? 0m;

            var standardPricePerSqm = modelAssumption?.StandardPrice ?? 0m;
            var coverageAmount = modelAssumption?.CoverageAmount;
            var usableArea = unit.UsableArea ?? 0m;
            var standardPrice = standardPricePerSqm * usableArea;

            var landArea = unit.LandArea ?? 0m;
            var landIncreaseDecreaseRate = assumption.LandIncreaseDecreaseRate ?? 0m;
            var landIncreaseDecreaseAmount = (landArea - standardLandArea) * landIncreaseDecreaseRate;

            var adjustPriceLocation = CalculateLBLocationAdjustment(assumption, unitPrice, standardPrice, usableArea);

            var totalAppraisalValue = standardPrice + landIncreaseDecreaseAmount + adjustPriceLocation;
            var totalAppraisalValueRounded = RoundToNearest10000(totalAppraisalValue);
            var forceSellingPrice = assumption.ForceSalePercentage.HasValue
                ? Math.Round(totalAppraisalValueRounded * assumption.ForceSalePercentage.Value / 100m, 0)
                : (decimal?)null;

            unitPrice.UpdateLandAndBuildingCalculatedValues(
                landIncreaseDecreaseAmount, adjustPriceLocation, standardPrice,
                totalAppraisalValue, totalAppraisalValueRounded, forceSellingPrice, coverageAmount);

            results.Add(unitPrice);
        }

        return results.AsReadOnly();
    }

    private static decimal CalculateLBLocationAdjustment(
        ProjectPricingAssumption assumption,
        ProjectUnitPrice unitPrice,
        decimal standardPrice,
        decimal usableArea)
    {
        var rawAdjustment = 0m;
        if (unitPrice.IsCorner) rawAdjustment += assumption.CornerAdjustment ?? 0m;
        if (unitPrice.IsEdge) rawAdjustment += assumption.EdgeAdjustment ?? 0m;
        if (unitPrice.IsNearGarden) rawAdjustment += assumption.NearGardenAdjustment ?? 0m;
        if (unitPrice.IsOther) rawAdjustment += assumption.OtherAdjustment ?? 0m;

        return assumption.LocationMethod switch
        {
            "AdjustPriceSqm" => rawAdjustment * usableArea,
            "AdjustPricePercentage" => standardPrice * rawAdjustment / 100m,
            _ => rawAdjustment
        };
    }

    private static decimal RoundToNearest10000(decimal value)
        => Math.Round(value / 10000m, MidpointRounding.AwayFromZero) * 10000m;

    // =========================================================================
    // Type guards
    // =========================================================================

    private void RequireCondo(string operationName)
    {
        if (ProjectType != ProjectType.Condo)
            throw new InvalidProjectStateException(
                $"Operation '{operationName}' is only valid for Condo projects. Current type: {ProjectType}.");
    }

    private void RequireLandAndBuilding(string operationName)
    {
        if (ProjectType != ProjectType.LandAndBuilding)
            throw new InvalidProjectStateException(
                $"Operation '{operationName}' is only valid for LandAndBuilding projects. Current type: {ProjectType}.");
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private void AutoCreateCondoTowersAndModels()
    {
        // Auto-create placeholder towers from unique names in uploaded units
        var towerNames = _units
            .Where(u => !string.IsNullOrWhiteSpace(u.TowerName))
            .Select(u => u.TowerName!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in towerNames)
        {
            if (!_towers.Any(t => string.Equals(t.TowerName, name, StringComparison.OrdinalIgnoreCase)))
                _towers.Add(ProjectTower.Create(Id, name));
        }

        // Auto-create placeholder models from unique model types
        var modelTypes = _units
            .Where(u => !string.IsNullOrWhiteSpace(u.ModelType))
            .Select(u => u.ModelType!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in modelTypes)
        {
            if (!_models.Any(m => string.Equals(m.ModelName, name, StringComparison.OrdinalIgnoreCase)))
                _models.Add(ProjectModel.Create(Id, name));
        }
    }

    private void LinkCondoUnitsToTowersAndModels()
    {
        foreach (var unit in _units)
        {
            if (!string.IsNullOrWhiteSpace(unit.TowerName))
            {
                var tower = _towers.First(t =>
                    string.Equals(t.TowerName, unit.TowerName, StringComparison.OrdinalIgnoreCase));
                unit.SetProjectTowerId(tower.Id);
            }

            if (!string.IsNullOrWhiteSpace(unit.ModelType))
            {
                var model = _models.First(m =>
                    string.Equals(m.ModelName, unit.ModelType, StringComparison.OrdinalIgnoreCase));
                unit.SetProjectModelId(model.Id);
            }
        }
    }

    private void AutoCreateLandAndBuildingModels()
    {
        // VillageUnit uses ModelName for its model type reference
        var modelNames = _units
            .Where(u => !string.IsNullOrWhiteSpace(u.ModelType))
            .Select(u => u.ModelType!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var name in modelNames)
        {
            if (!_models.Any(m => string.Equals(m.ModelName, name, StringComparison.OrdinalIgnoreCase)))
                _models.Add(ProjectModel.Create(Id, name));
        }
    }

    private void LinkLandAndBuildingUnitsToModels()
    {
        foreach (var unit in _units)
        {
            if (!string.IsNullOrWhiteSpace(unit.ModelType))
            {
                var model = _models.First(m =>
                    string.Equals(m.ModelName, unit.ModelType, StringComparison.OrdinalIgnoreCase));
                unit.SetProjectModelId(model.Id);
            }
        }
    }

    // =========================================================================
    // Shared validation helpers
    // =========================================================================

    private static void ValidateSharedFields(
        decimal? landAreaRai, decimal? landAreaNgan, decimal? landAreaWa,
        int? unitForSaleCount, int? numberOfPhase)
    {
        if (landAreaRai is < 0)
            throw new ArgumentException("Land area (Rai) cannot be negative", nameof(landAreaRai));
        if (landAreaNgan is < 0)
            throw new ArgumentException("Land area (Ngan) cannot be negative", nameof(landAreaNgan));
        if (landAreaWa is < 0)
            throw new ArgumentException("Land area (Wa) cannot be negative", nameof(landAreaWa));
        if (unitForSaleCount is < 0)
            throw new ArgumentException("Unit for sale count cannot be negative", nameof(unitForSaleCount));
        if (numberOfPhase is < 0)
            throw new ArgumentException("Number of phases cannot be negative", nameof(numberOfPhase));
    }

    private static void ValidateTypeSpecificFields(
        ProjectType projectType,
        string? builtOnTitleDeedNumber,
        DateTime? licenseExpirationDate)
    {
        if (projectType == ProjectType.LandAndBuilding && builtOnTitleDeedNumber != null)
            throw new ArgumentException(
                "BuiltOnTitleDeedNumber is only applicable to Condo projects.",
                nameof(builtOnTitleDeedNumber));

        if (projectType == ProjectType.Condo && licenseExpirationDate != null)
            throw new ArgumentException(
                "LicenseExpirationDate is only applicable to LandAndBuilding projects.",
                nameof(licenseExpirationDate));
    }
}
