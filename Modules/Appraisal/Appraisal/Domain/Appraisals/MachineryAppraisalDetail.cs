namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Machinery property appraisal details including condition and appraiser assessment.
/// 1:1 relationship with AppraisalProperty (PropertyType = Machinery)
/// </summary>
public class MachineryAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Machine Identification
    public string? PropertyName { get; private set; }
    public string? MachineName { get; private set; }
    public string? EngineNo { get; private set; }
    public string? ChassisNo { get; private set; }
    public string? RegistrationNo { get; private set; }

    // Machine Specifications
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public int? YearOfManufacture { get; private set; }
    public string? CountryOfManufacture { get; private set; }

    // Purchase Info
    public DateTime? PurchaseDate { get; private set; }
    public decimal? PurchasePrice { get; private set; }

    // Dimensions
    public string? Capacity { get; private set; }
    public decimal? Width { get; private set; }
    public decimal? Length { get; private set; }
    public decimal? Height { get; private set; }

    // Energy
    public string? EnergyUse { get; private set; }
    public string? EnergyUseRemark { get; private set; }

    // Owner
    public string OwnerName { get; private set; } = null!;
    public bool IsOwnerVerified { get; private set; }

    // Usage & Condition
    public bool CanUse { get; private set; } = true;
    public string? Location { get; private set; }
    public string? ConditionUse { get; private set; }
    public string? MachineCondition { get; private set; }
    public int? MachineAge { get; private set; }
    public string? MachineEfficiency { get; private set; }
    public string? MachineTechnology { get; private set; }
    public string? UsePurpose { get; private set; }
    public string? MachinePart { get; private set; }

    // Appraiser Notes
    public string? Remark { get; private set; }
    public string? Other { get; private set; }
    public string? AppraiserOpinion { get; private set; }

    private MachineryAppraisalDetail()
    {
    }

    public static MachineryAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        Guid createdBy)
    {
        return new MachineryAppraisalDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName,
            CanUse = true
        };
    }

    public void Update(
        // Machine Identification
        string? propertyName = null,
        string? machineName = null,
        string? engineNo = null,
        string? chassisNo = null,
        string? registrationNo = null,
        // Machine Specifications
        string? brand = null,
        string? model = null,
        int? yearOfManufacture = null,
        string? countryOfManufacture = null,
        // Purchase Info
        DateTime? purchaseDate = null,
        decimal? purchasePrice = null,
        // Dimensions
        string? capacity = null,
        decimal? width = null,
        decimal? length = null,
        decimal? height = null,
        // Energy
        string? energyUse = null,
        string? energyUseRemark = null,
        // Owner
        string? ownerName = null,
        bool? isOwnerVerified = null,
        // Usage & Condition
        bool? canUse = null,
        string? location = null,
        string? conditionUse = null,
        string? machineCondition = null,
        int? machineAge = null,
        string? machineEfficiency = null,
        string? machineTechnology = null,
        string? usePurpose = null,
        string? machinePart = null,
        // Appraiser Notes
        string? remark = null,
        string? other = null,
        string? appraiserOpinion = null)
    {
        // Machine Identification
        if (propertyName is not null) PropertyName = propertyName;
        if (machineName is not null) MachineName = machineName;
        if (engineNo is not null) EngineNo = engineNo;
        if (chassisNo is not null) ChassisNo = chassisNo;
        if (registrationNo is not null) RegistrationNo = registrationNo;

        // Machine Specifications
        if (brand is not null) Brand = brand;
        if (model is not null) Model = model;
        if (yearOfManufacture.HasValue) YearOfManufacture = yearOfManufacture.Value;
        if (countryOfManufacture is not null) CountryOfManufacture = countryOfManufacture;

        // Purchase Info
        if (purchaseDate.HasValue) PurchaseDate = purchaseDate.Value;
        if (purchasePrice.HasValue) PurchasePrice = purchasePrice.Value;

        // Dimensions
        if (capacity is not null) Capacity = capacity;
        if (width.HasValue) Width = width.Value;
        if (length.HasValue) Length = length.Value;
        if (height.HasValue) Height = height.Value;

        // Energy
        if (energyUse is not null) EnergyUse = energyUse;
        if (energyUseRemark is not null) EnergyUseRemark = energyUseRemark;

        // Owner
        if (ownerName is not null) OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;

        // Usage & Condition
        if (canUse.HasValue) CanUse = canUse.Value;
        if (location is not null) Location = location;
        if (conditionUse is not null) ConditionUse = conditionUse;
        if (machineCondition is not null) MachineCondition = machineCondition;
        if (machineAge.HasValue) MachineAge = machineAge.Value;
        if (machineEfficiency is not null) MachineEfficiency = machineEfficiency;
        if (machineTechnology is not null) MachineTechnology = machineTechnology;
        if (usePurpose is not null) UsePurpose = usePurpose;
        if (machinePart is not null) MachinePart = machinePart;

        // Appraiser Notes
        if (remark is not null) Remark = remark;
        if (other is not null) Other = other;
        if (appraiserOpinion is not null) AppraiserOpinion = appraiserOpinion;
    }
}
