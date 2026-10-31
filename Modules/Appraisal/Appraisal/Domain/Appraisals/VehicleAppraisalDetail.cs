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
    public string? LicensePlateNumber { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public DateTime? RegistrationDate { get; private set; }
    public string? Color { get; private set; }
    public string? ColorOther { get; private set; }

    // Vehicle Specifications
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public int? YearOfManufacture { get; private set; }
    public string? CountryOfManufacture { get; private set; }
    public string? VehicleType { get; private set; }
    public string? VehicleTypeOther { get; private set; }

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
    public string? OwnerName { get; private set; }
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
        Guid appraisalPropertyId)
    {
        return new VehicleAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId,
        };
    }

    public static VehicleAppraisalDetail CopyFrom(VehicleAppraisalDetail source, Guid newPropertyId)
    {
        return new VehicleAppraisalDetail
        {
            AppraisalPropertyId = newPropertyId,
            PropertyName = source.PropertyName,
            VehicleName = source.VehicleName,
            EngineNo = source.EngineNo,
            ChassisNo = source.ChassisNo,
            LicensePlateNumber = source.LicensePlateNumber,
            RegistrationNumber = source.RegistrationNumber,
            RegistrationDate = source.RegistrationDate,
            Color = source.Color,
            ColorOther = source.ColorOther,
            Brand = source.Brand,
            Model = source.Model,
            YearOfManufacture = source.YearOfManufacture,
            CountryOfManufacture = source.CountryOfManufacture,
            VehicleType = source.VehicleType,
            VehicleTypeOther = source.VehicleTypeOther,
            PurchaseDate = source.PurchaseDate,
            PurchasePrice = source.PurchasePrice,
            Capacity = source.Capacity,
            Width = source.Width,
            Length = source.Length,
            Height = source.Height,
            EnergyUse = source.EnergyUse,
            EnergyUseRemark = source.EnergyUseRemark,
            OwnerName = source.OwnerName,
            IsOwnerVerified = source.IsOwnerVerified,
            CanUse = source.CanUse,
            Location = source.Location,
            ConditionUse = source.ConditionUse,
            VehicleCondition = source.VehicleCondition,
            VehicleAge = source.VehicleAge,
            VehicleEfficiency = source.VehicleEfficiency,
            VehicleTechnology = source.VehicleTechnology,
            UsePurpose = source.UsePurpose,
            VehiclePart = source.VehiclePart,
            Remark = source.Remark,
            Other = source.Other,
            AppraiserOpinion = source.AppraiserOpinion
        };
    }

    public void Update(
        // Vehicle Identification
        string? propertyName = null,
        string? vehicleName = null,
        string? engineNo = null,
        string? chassisNo = null,
        string? licensePlateNumber = null,
        string? registrationNumber = null,
        DateTime? registrationDate = null,
        string? color = null,
        string? colorOther = null,
        // Vehicle Specifications
        string? brand = null,
        string? model = null,
        int? yearOfManufacture = null,
        string? countryOfManufacture = null,
        string? vehicleType = null,
        string? vehicleTypeOther = null,
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
        PropertyName = propertyName;
        VehicleName = vehicleName;
        EngineNo = engineNo;
        ChassisNo = chassisNo;
        LicensePlateNumber = licensePlateNumber;
        RegistrationNumber = registrationNumber;
        RegistrationDate = registrationDate;
        Color = color;
        ColorOther = colorOther;

        // Vehicle Specifications
        Brand = brand;
        Model = model;
        YearOfManufacture = yearOfManufacture;
        CountryOfManufacture = countryOfManufacture;
        VehicleType = vehicleType;
        VehicleTypeOther = vehicleTypeOther;

        // Purchase Info
        PurchaseDate = purchaseDate;
        PurchasePrice = purchasePrice;

        // Dimensions
        Capacity = capacity;
        Width = width;
        Length = length;
        Height = height;

        // Energy
        EnergyUse = energyUse;
        EnergyUseRemark = energyUseRemark;

        // Owner
        OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;

        // Usage & Condition
        if (canUse.HasValue) CanUse = canUse.Value;
        Location = location;
        ConditionUse = conditionUse;
        VehicleCondition = vehicleCondition;
        VehicleAge = vehicleAge;
        VehicleEfficiency = vehicleEfficiency;
        VehicleTechnology = vehicleTechnology;
        UsePurpose = usePurpose;
        VehiclePart = vehiclePart;

        // Appraiser Notes
        Remark = remark;
        Other = other;
        AppraiserOpinion = appraiserOpinion;
    }
}
