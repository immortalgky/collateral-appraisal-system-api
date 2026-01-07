namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Vehicle property appraisal details including condition and appraiser assessment.
/// 1:1 relationship with AppraisalProperty (PropertyType = Vehicle)
/// </summary>
public class VehicleAppraisalDetail : Entity<Guid>
{
    // Foreign Key - 1:1 with AppraisalProperties
    public Guid AppraisalPropertyId { get; private set; }

    // Vehicle Identification
    public string? PropertyName { get; private set; }
    public string? VehicleName { get; private set; }
    public string? EngineNo { get; private set; }
    public string? ChassisNo { get; private set; }
    public string? RegistrationNo { get; private set; }

    // Vehicle Specifications
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
    public string? VehicleCondition { get; private set; }
    public int? VehicleAge { get; private set; }
    public string? VehicleEfficiency { get; private set; }
    public string? VehicleTechnology { get; private set; }
    public string? UsePurpose { get; private set; }
    public string? VehiclePart { get; private set; }

    // Appraiser Notes
    public string? Remark { get; private set; }
    public string? Other { get; private set; }
    public string? AppraiserOpinion { get; private set; }

    private VehicleAppraisalDetail()
    {
    }

    public static VehicleAppraisalDetail Create(
        Guid appraisalPropertyId,
        string ownerName,
        Guid createdBy)
    {
        return new VehicleAppraisalDetail
        {
            Id = Guid.NewGuid(),
            AppraisalPropertyId = appraisalPropertyId,
            OwnerName = ownerName,
            CanUse = true
        };
    }

    public void Update(
        // Vehicle Identification
        string? propertyName = null,
        string? vehicleName = null,
        string? engineNo = null,
        string? chassisNo = null,
        string? registrationNo = null,
        // Vehicle Specifications
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
        string? vehicleCondition = null,
        int? vehicleAge = null,
        string? vehicleEfficiency = null,
        string? vehicleTechnology = null,
        string? usePurpose = null,
        string? vehiclePart = null,
        // Appraiser Notes
        string? remark = null,
        string? other = null,
        string? appraiserOpinion = null)
    {
        // Vehicle Identification
        if (propertyName is not null) PropertyName = propertyName;
        if (vehicleName is not null) VehicleName = vehicleName;
        if (engineNo is not null) EngineNo = engineNo;
        if (chassisNo is not null) ChassisNo = chassisNo;
        if (registrationNo is not null) RegistrationNo = registrationNo;

        // Vehicle Specifications
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
        if (vehicleCondition is not null) VehicleCondition = vehicleCondition;
        if (vehicleAge.HasValue) VehicleAge = vehicleAge.Value;
        if (vehicleEfficiency is not null) VehicleEfficiency = vehicleEfficiency;
        if (vehicleTechnology is not null) VehicleTechnology = vehicleTechnology;
        if (usePurpose is not null) UsePurpose = usePurpose;
        if (vehiclePart is not null) VehiclePart = vehiclePart;

        // Appraiser Notes
        if (remark is not null) Remark = remark;
        if (other is not null) Other = other;
        if (appraiserOpinion is not null) AppraiserOpinion = appraiserOpinion;
    }
}
