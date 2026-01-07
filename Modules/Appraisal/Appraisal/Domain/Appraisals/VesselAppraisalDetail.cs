namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Vessel property appraisal details including condition and appraiser assessment.
/// 1:1 relationship with AppraisalProperty (PropertyType = Vessel)
/// </summary>
public class VesselAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Vessel Identification
    public string? PropertyName { get; private set; }
    public string? VesselName { get; private set; }
    public string? EngineNo { get; private set; }
    public string? RegistrationNo { get; private set; }
    public DateTime? RegistrationDate { get; private set; }

    // Vessel Specifications
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public int? YearOfManufacture { get; private set; }
    public string? PlaceOfManufacture { get; private set; }
    public string? VesselType { get; private set; }
    public string? ClassOfVessel { get; private set; }

    // Purchase Info
    public DateTime? PurchaseDate { get; private set; }
    public decimal? PurchasePrice { get; private set; }

    // Dimensions
    public string? EngineCapacity { get; private set; }
    public decimal? Width { get; private set; }
    public decimal? Length { get; private set; }
    public decimal? Height { get; private set; }
    public decimal? GrossTonnage { get; private set; }
    public decimal? NetTonnage { get; private set; }

    // Energy
    public string? EnergyUse { get; private set; }
    public string? EnergyUseRemark { get; private set; }

    // Owner
    public string OwnerName { get; private set; } = null!;
    public bool IsOwnerVerified { get; private set; }

    // Vessel Info
    public bool CanUse { get; private set; } = true;
    public string? FormerName { get; private set; }
    public string? VesselCurrentName { get; private set; }
    public string? Location { get; private set; }

    // Condition & Assessment
    public string? ConditionUse { get; private set; }
    public string? VesselCondition { get; private set; }
    public int? VesselAge { get; private set; }
    public string? VesselEfficiency { get; private set; }
    public string? VesselTechnology { get; private set; }
    public string? UsePurpose { get; private set; }
    public string? VesselPart { get; private set; }

    // Appraiser Notes
    public string? Remark { get; private set; }
    public string? Other { get; private set; }
    public string? AppraiserOpinion { get; private set; }

    private VesselAppraisalDetail()
    {
    }

    public static VesselAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        Guid createdBy)
    {
        return new VesselAppraisalDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName,
            CanUse = true
        };
    }

    public void Update(
        // Vessel Identification
        string? propertyName = null,
        string? vesselName = null,
        string? engineNo = null,
        string? registrationNo = null,
        DateTime? registrationDate = null,
        // Vessel Specifications
        string? brand = null,
        string? model = null,
        int? yearOfManufacture = null,
        string? placeOfManufacture = null,
        string? vesselType = null,
        string? classOfVessel = null,
        // Purchase Info
        DateTime? purchaseDate = null,
        decimal? purchasePrice = null,
        // Dimensions
        string? engineCapacity = null,
        decimal? width = null,
        decimal? length = null,
        decimal? height = null,
        decimal? grossTonnage = null,
        decimal? netTonnage = null,
        // Energy
        string? energyUse = null,
        string? energyUseRemark = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        // Vessel Info
        bool? canUse = null,
        string? formerName = null,
        string? vesselCurrentName = null,
        string? location = null,
        // Condition & Assessment
        string? conditionUse = null,
        string? vesselCondition = null,
        int? vesselAge = null,
        string? vesselEfficiency = null,
        string? vesselTechnology = null,
        string? usePurpose = null,
        string? vesselPart = null,
        // Appraiser Notes
        string? remark = null,
        string? other = null,
        string? appraiserOpinion = null)
    {
        // Vessel Identification
        if (propertyName is not null) PropertyName = propertyName;
        if (vesselName is not null) VesselName = vesselName;
        if (engineNo is not null) EngineNo = engineNo;
        if (registrationNo is not null) RegistrationNo = registrationNo;
        if (registrationDate.HasValue) RegistrationDate = registrationDate.Value;

        // Vessel Specifications
        if (brand is not null) Brand = brand;
        if (model is not null) Model = model;
        if (yearOfManufacture.HasValue) YearOfManufacture = yearOfManufacture.Value;
        if (placeOfManufacture is not null) PlaceOfManufacture = placeOfManufacture;
        if (vesselType is not null) VesselType = vesselType;
        if (classOfVessel is not null) ClassOfVessel = classOfVessel;

        // Purchase Info
        if (purchaseDate.HasValue) PurchaseDate = purchaseDate.Value;
        if (purchasePrice.HasValue) PurchasePrice = purchasePrice.Value;

        // Dimensions
        if (engineCapacity is not null) EngineCapacity = engineCapacity;
        if (width.HasValue) Width = width.Value;
        if (length.HasValue) Length = length.Value;
        if (height.HasValue) Height = height.Value;
        if (grossTonnage.HasValue) GrossTonnage = grossTonnage.Value;
        if (netTonnage.HasValue) NetTonnage = netTonnage.Value;

        // Energy
        if (energyUse is not null) EnergyUse = energyUse;
        if (energyUseRemark is not null) EnergyUseRemark = energyUseRemark;

        // Owner
        if (ownerName is not null) OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;

        // Vessel Info
        if (canUse.HasValue) CanUse = canUse.Value;
        if (formerName is not null) FormerName = formerName;
        if (vesselCurrentName is not null) VesselCurrentName = vesselCurrentName;
        if (location is not null) Location = location;

        // Condition & Assessment
        if (conditionUse is not null) ConditionUse = conditionUse;
        if (vesselCondition is not null) VesselCondition = vesselCondition;
        if (vesselAge.HasValue) VesselAge = vesselAge.Value;
        if (vesselEfficiency is not null) VesselEfficiency = vesselEfficiency;
        if (vesselTechnology is not null) VesselTechnology = vesselTechnology;
        if (usePurpose is not null) UsePurpose = usePurpose;
        if (vesselPart is not null) VesselPart = vesselPart;

        // Appraiser Notes
        if (remark is not null) Remark = remark;
        if (other is not null) Other = other;
        if (appraiserOpinion is not null) AppraiserOpinion = appraiserOpinion;
    }
}
