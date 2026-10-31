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
    public string? RegistrationNumber { get; private set; }
    public string? SerialNo { get; private set; }

    // Registration & Installation — carried over from the request title (MachineInfo) and
    // editable by the appraiser. Codes come from the GeneralParameters master:
    // InstallationStatus = group "MachineStatus" (1 = installed, 2 = under procurement),
    // MachineType = group "MachineType" (1..3).
    public bool RegistrationStatus { get; private set; }
    public string? InstallationStatus { get; private set; }
    public string? MachineType { get; private set; }
    public string? MachineTypeOther { get; private set; }
    public string? InvoiceNumber { get; private set; }

    /// <summary>
    /// Price certification for THIS machine — the same decision the machinery summary report
    /// prints as "(ไม่ประเมินมูลค่า)". Distinct from AppraisalDecision.IsPriceVerified, which is
    /// appraisal-wide and zeroes the valuation totals; this one is descriptive and does not touch
    /// pricing. Entirely the appraiser's call: registration and installation status no longer
    /// constrain it.
    /// </summary>
    public bool IsPriceCertified { get; private set; } = true;

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
    public decimal? MachineAge { get; private set; }
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
            RegistrationNumber = source.RegistrationNumber,
            SerialNo = source.SerialNo,
            RegistrationStatus = source.RegistrationStatus,
            InstallationStatus = source.InstallationStatus,
            MachineType = source.MachineType,
            MachineTypeOther = source.MachineTypeOther,
            InvoiceNumber = source.InvoiceNumber,
            IsPriceCertified = source.IsPriceCertified,
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
        string? registrationNumber = null,
        string? serialNo = null,
        // Registration & Installation
        bool? registrationStatus = null,
        string? installationStatus = null,
        string? machineType = null,
        string? machineTypeOther = null,
        string? invoiceNumber = null,
        bool? isPriceCertified = null,
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
        decimal? machineAge = null,
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
        PropertyName = propertyName;
        MachineName = machineName;
        EngineNo = engineNo;
        ChassisNo = chassisNo;
        RegistrationNumber = registrationNumber;
        SerialNo = serialNo;

        // Registration & Installation
        if (registrationStatus.HasValue) RegistrationStatus = registrationStatus.Value;
        InstallationStatus = installationStatus;
        MachineType = machineType;
        MachineTypeOther = machineTypeOther;
        InvoiceNumber = invoiceNumber;
        if (isPriceCertified.HasValue) IsPriceCertified = isPriceCertified.Value;

        // Machine Specifications
        Brand = brand;
        Model = model;
        Series = series;
        YearOfManufacture = yearOfManufacture;
        Manufacturer = manufacturer;

        // Purchase Info
        PurchaseDate = purchaseDate;
        PurchasePrice = purchasePrice;

        // Dimensions
        Capacity = capacity;
        Quantity = quantity;
        MachineDimensions = machineDimensions;
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
        if (isOperational.HasValue) IsOperational = isOperational.Value;
        Location = location;
        ConditionUse = conditionUse;
        MachineCondition = machineCondition;
        MachineAge = machineAge;
        MachineEfficiency = machineEfficiency;
        MachineTechnology = machineTechnology;
        UsagePurpose = usagePurpose;
        MachineParts = machineParts;

        // Valuation
        ReplacementValue = replacementValue;
        ConditionValue = conditionValue;

        // Appraiser Notes
        Remark = remark;
        Other = other;
        AppraiserOpinion = appraiserOpinion;

        ClearInapplicableFields();
    }

    /// <summary>MachineStatus parameter code for "under procurement".</summary>
    private const string UnderProcurementStatus = "2";

    /// <summary>
    /// A machine that is not registered has no registration number, and one that is not being
    /// procured has no quotation it was priced from — so those columns are emptied rather than
    /// left holding a value the record contradicts.
    ///
    /// Enforced here, not in the form, so it holds for every entry point: create, update, admin
    /// correction, and anything that reaches the aggregate later. The form disables the same two
    /// boxes for the same reasons; this is what makes the database agree with what it shows.
    ///
    /// Note what this is NOT: <see cref="IsPriceCertified"/> is left alone. Certifying a price is
    /// the appraiser's judgement, and an invariant that inferred it from these same two fields was
    /// removed deliberately.
    /// </summary>
    private void ClearInapplicableFields()
    {
        if (!RegistrationStatus) RegistrationNumber = null;
        if (InstallationStatus != UnderProcurementStatus) InvoiceNumber = null;
    }

    /// <summary>
    /// Records what actually happened to a field the invariant may have overwritten: nothing at
    /// all if it ended where it started, and the real before/after otherwise — including a
    /// requested value the invariant then cleared, which is a change, just not the one asked for.
    /// </summary>
    private static void ReportNetChange(
        string key,
        string? before,
        string? after,
        Dictionary<string, object?> diff)
    {
        if (before == after)
            diff.Remove(key);
        else
            diff[key] = new { from = before, to = after };
    }
}
