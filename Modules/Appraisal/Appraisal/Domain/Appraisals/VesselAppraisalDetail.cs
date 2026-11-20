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
    public string? HullNumber { get; private set; }
    public string? RegistrationNumber { get; private set; }
    public DateTime? RegistrationDate { get; private set; }

    // Vessel Specifications
    public string? Brand { get; private set; }
    public string? Model { get; private set; }
    public int? YearOfManufacture { get; private set; }
    public string? PlaceOfManufacture { get; private set; }
    public string? VesselType { get; private set; }
    public string? VesselTypeOther { get; private set; }
    public string? ClassOfVessel { get; private set; }
    public string? EnginePower { get; private set; }
    public decimal? NumberOfEngines { get; private set; }

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
    public string? OwnerName { get; private set; }
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
        Guid appraisalPropertyId)
    {
        return new VesselAppraisalDetail
        {
            AppraisalPropertyId = appraisalPropertyId,
        };
    }

    public static VesselAppraisalDetail CopyFrom(VesselAppraisalDetail source, Guid newPropertyId)
    {
        return new VesselAppraisalDetail
        {
            AppraisalPropertyId = newPropertyId,
            PropertyName = source.PropertyName,
            VesselName = source.VesselName,
            EngineNo = source.EngineNo,
            HullNumber = source.HullNumber,
            RegistrationNumber = source.RegistrationNumber,
            RegistrationDate = source.RegistrationDate,
            Brand = source.Brand,
            Model = source.Model,
            YearOfManufacture = source.YearOfManufacture,
            PlaceOfManufacture = source.PlaceOfManufacture,
            VesselType = source.VesselType,
            VesselTypeOther = source.VesselTypeOther,
            ClassOfVessel = source.ClassOfVessel,
            EnginePower = source.EnginePower,
            NumberOfEngines = source.NumberOfEngines,
            PurchaseDate = source.PurchaseDate,
            PurchasePrice = source.PurchasePrice,
            EngineCapacity = source.EngineCapacity,
            Width = source.Width,
            Length = source.Length,
            Height = source.Height,
            GrossTonnage = source.GrossTonnage,
            NetTonnage = source.NetTonnage,
            EnergyUse = source.EnergyUse,
            EnergyUseRemark = source.EnergyUseRemark,
            OwnerName = source.OwnerName,
            IsOwnerVerified = source.IsOwnerVerified,
            CanUse = source.CanUse,
            FormerName = source.FormerName,
            VesselCurrentName = source.VesselCurrentName,
            Location = source.Location,
            ConditionUse = source.ConditionUse,
            VesselCondition = source.VesselCondition,
            VesselAge = source.VesselAge,
            VesselEfficiency = source.VesselEfficiency,
            VesselTechnology = source.VesselTechnology,
            UsePurpose = source.UsePurpose,
            VesselPart = source.VesselPart,
            Remark = source.Remark,
            Other = source.Other,
            AppraiserOpinion = source.AppraiserOpinion
        };
    }

    public void Update(
        // Vessel Identification
        string? propertyName = null,
        string? vesselName = null,
        string? engineNo = null,
        string? hullNumber = null,
        string? registrationNumber = null,
        DateTime? registrationDate = null,
        // Vessel Specifications
        string? brand = null,
        string? model = null,
        int? yearOfManufacture = null,
        string? placeOfManufacture = null,
        string? vesselType = null,
        string? vesselTypeOther = null,
        string? classOfVessel = null,
        string? enginePower = null,
        decimal? numberOfEngines = null,
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
        PropertyName = propertyName;
        VesselName = vesselName;
        EngineNo = engineNo;
        HullNumber = hullNumber;
        RegistrationNumber = registrationNumber;
        RegistrationDate = registrationDate;

        // Vessel Specifications
        Brand = brand;
        Model = model;
        YearOfManufacture = yearOfManufacture;
        PlaceOfManufacture = placeOfManufacture;
        VesselType = vesselType;
        VesselTypeOther = vesselTypeOther;
        ClassOfVessel = classOfVessel;
        EnginePower = enginePower;
        NumberOfEngines = numberOfEngines;

        // Purchase Info
        PurchaseDate = purchaseDate;
        PurchasePrice = purchasePrice;

        // Dimensions
        EngineCapacity = engineCapacity;
        Width = width;
        Length = length;
        Height = height;
        GrossTonnage = grossTonnage;
        NetTonnage = netTonnage;

        // Energy
        EnergyUse = energyUse;
        EnergyUseRemark = energyUseRemark;

        // Owner
        OwnerName = ownerName;
        if (isOwnerVerified.HasValue) IsOwnerVerified = isOwnerVerified.Value;

        // Vessel Info
        if (canUse.HasValue) CanUse = canUse.Value;
        FormerName = formerName;
        VesselCurrentName = vesselCurrentName;
        Location = location;

        // Condition & Assessment
        ConditionUse = conditionUse;
        VesselCondition = vesselCondition;
        VesselAge = vesselAge;
        VesselEfficiency = vesselEfficiency;
        VesselTechnology = vesselTechnology;
        UsePurpose = usePurpose;
        VesselPart = vesselPart;

        // Appraiser Notes
        Remark = remark;
        Other = other;
        AppraiserOpinion = appraiserOpinion;
    }
}
