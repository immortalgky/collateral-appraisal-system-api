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
    public string? Series { get; private set; }
    public int? YearOfManufacture { get; private set; }
    public string? Manufacturer { get; private set; }

    // Purchase Info
    public DateTime? PurchaseDate { get; private set; }
    public decimal? PurchasePrice { get; private set; }

    // Dimensions
    public string? Capacity { get; private set; }
    public int? Quantity { get; private set; }
    public string? MachineDimensions { get; private set; }
    public decimal? Width { get; private set; }
    public decimal? Length { get; private set; }
    public decimal? Height { get; private set; }

    // Energy
    public string? EnergyUse { get; private set; }
    public string? EnergyUseRemark { get; private set; }

    // Owner
    public string? OwnerName { get; private set; }
    public bool IsOwnerVerified { get; private set; }

    // Usage & Condition
    public bool IsOperational { get; private set; } = true;
    public string? Location { get; private set; }
    public string? ConditionUse { get; private set; }
    public string? MachineCondition { get; private set; }
    public int? MachineAge { get; private set; }
    public string? MachineEfficiency { get; private set; }
    public string? MachineTechnology { get; private set; }
    public string? UsagePurpose { get; private set; }
    public string? MachineParts { get; private set; }

    // Valuation
    public decimal? ReplacementValue { get; private set; }
    public decimal? ConditionValue { get; private set; }

    // Appraiser Notes
    public string? Remark { get; private set; }
    public string? Other { get; private set; }
    public string? AppraiserOpinion { get; private set; }

    private MachineryAppraisalDetail()
    {
        // For EF Core
    }

    public static MachineryAppraisalDetail Create(
        Guid appraisalPropertyId)
    {
        return new MachineryAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId
        };
    }

    public static MachineryAppraisalDetail CopyFrom(MachineryAppraisalDetail source, Guid newPropertyId)
    {
        return new MachineryAppraisalDetail
        {
            AppraisalPropertyId = newPropertyId,
            PropertyName = source.PropertyName,
            MachineName = source.MachineName,
            EngineNo = source.EngineNo,
            ChassisNo = source.ChassisNo,
            RegistrationNo = source.RegistrationNo,
            Brand = source.Brand,
            Model = source.Model,
            Series = source.Series,
            YearOfManufacture = source.YearOfManufacture,
            Manufacturer = source.Manufacturer,
            PurchaseDate = source.PurchaseDate,
            PurchasePrice = source.PurchasePrice,
            Capacity = source.Capacity,
            Quantity = source.Quantity,
            MachineDimensions = source.MachineDimensions,
            Width = source.Width,
            Length = source.Length,
            Height = source.Height,
            EnergyUse = source.EnergyUse,
            EnergyUseRemark = source.EnergyUseRemark,
            OwnerName = source.OwnerName,
            IsOwnerVerified = source.IsOwnerVerified,
            IsOperational = source.IsOperational,
            Location = source.Location,
            ConditionUse = source.ConditionUse,
            MachineCondition = source.MachineCondition,
            MachineAge = source.MachineAge,
            MachineEfficiency = source.MachineEfficiency,
            MachineTechnology = source.MachineTechnology,
            UsagePurpose = source.UsagePurpose,
            MachineParts = source.MachineParts,
            ReplacementValue = source.ReplacementValue,
            ConditionValue = source.ConditionValue,
            Remark = source.Remark,
            Other = source.Other,
            AppraiserOpinion = source.AppraiserOpinion
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
        string? series = null,
        int? yearOfManufacture = null,
        string? manufacturer = null,
        // Purchase Info
        DateTime? purchaseDate = null,
        decimal? purchasePrice = null,
        // Dimensions
        string? capacity = null,
        int? quantity = null,
        string? machineDimensions = null,
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
        bool? isOperational = null,
        string? location = null,
        string? conditionUse = null,
        string? machineCondition = null,
        int? machineAge = null,
        string? machineEfficiency = null,
        string? machineTechnology = null,
        string? usagePurpose = null,
        string? machineParts = null,
        // Valuation
        decimal? replacementValue = null,
        decimal? conditionValue = null,
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
        if (series is not null) Series = series;
        if (yearOfManufacture.HasValue) YearOfManufacture = yearOfManufacture.Value;
        if (manufacturer is not null) Manufacturer = manufacturer;

        // Purchase Info
        if (purchaseDate.HasValue) PurchaseDate = purchaseDate.Value;
        if (purchasePrice.HasValue) PurchasePrice = purchasePrice.Value;

        // Dimensions
        if (capacity is not null) Capacity = capacity;
        if (quantity.HasValue) Quantity = quantity.Value;
        if (machineDimensions is not null) MachineDimensions = machineDimensions;
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
        if (isOperational.HasValue) IsOperational = isOperational.Value;
        if (location is not null) Location = location;
        if (conditionUse is not null) ConditionUse = conditionUse;
        if (machineCondition is not null) MachineCondition = machineCondition;
        if (machineAge.HasValue) MachineAge = machineAge.Value;
        if (machineEfficiency is not null) MachineEfficiency = machineEfficiency;
        if (machineTechnology is not null) MachineTechnology = machineTechnology;
        if (usagePurpose is not null) UsagePurpose = usagePurpose;
        if (machineParts is not null) MachineParts = machineParts;

        // Valuation
        if (replacementValue.HasValue) ReplacementValue = replacementValue.Value;
        if (conditionValue.HasValue) ConditionValue = conditionValue.Value;

        // Appraiser Notes
        if (remark is not null) Remark = remark;
        if (other is not null) Other = other;
        if (appraiserOpinion is not null) AppraiserOpinion = appraiserOpinion;
    }
}