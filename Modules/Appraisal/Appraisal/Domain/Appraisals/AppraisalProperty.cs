namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Entity representing a property item within an appraisal.
/// Each property links to exactly one property detail table based on PropertyType.
/// </summary>
public class AppraisalProperty : Entity<Guid>
{
    // Core Properties
    public Guid AppraisalId { get; private set; }
    public int SequenceNumber { get; private set; }
    public PropertyType PropertyType { get; private set; } = null!;
    public string? Description { get; private set; }

    // Navigation Properties - 1:1 with detail tables (only one will be populated based on PropertyType)
    public LandAppraisalDetail? LandDetail { get; private set; }
    public BuildingAppraisalDetail? BuildingDetail { get; private set; }
    public CondoAppraisalDetail? CondoDetail { get; private set; }
    public LandAndBuildingAppraisalDetail? LandAndBuildingDetail { get; private set; }
    public VehicleAppraisalDetail? VehicleDetail { get; private set; }
    public VesselAppraisalDetail? VesselDetail { get; private set; }
    public MachineryAppraisalDetail? MachineryDetail { get; private set; }

    // Private constructor for EF Core
    private AppraisalProperty()
    {
    }

    // Private constructor for factory
    private AppraisalProperty(
        Guid appraisalId,
        int sequenceNumber,
        PropertyType propertyType,
        string? description)
    {
        //Id = Guid.NewGuid();
        AppraisalId = appraisalId;
        SequenceNumber = sequenceNumber;
        PropertyType = propertyType;
        Description = description;
    }

    /// <summary>
    /// Factory method to create a new property
    /// </summary>
    public static AppraisalProperty Create(
        Guid appraisalId,
        int sequenceNumber,
        string propertyTypeCode,
        string? description = null)
    {
        var propertyType = PropertyType.FromString(propertyTypeCode);
        return new AppraisalProperty(appraisalId, sequenceNumber, propertyType, description);
    }

    /// <summary>
    /// Update the sequence number (used when reordering)
    /// </summary>
    public void UpdateSequence(int newSequence)
    {
        SequenceNumber = newSequence;
    }

    /// <summary>
    /// Update the description
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    #region Set Detail Methods

    /// <summary>
    /// Set the land detail for this property
    /// </summary>
    public void SetLandDetail(LandAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.Land)
            throw new InvalidOperationException($"Cannot set land detail for property type '{PropertyType}'");

        LandDetail = detail;
    }

    /// <summary>
    /// Set the building detail for this property
    /// </summary>
    public void SetBuildingDetail(BuildingAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.Building)
            throw new InvalidOperationException($"Cannot set building detail for property type '{PropertyType}'");

        BuildingDetail = detail;
    }

    /// <summary>
    /// Set the condo detail for this property
    /// </summary>
    public void SetCondoDetail(CondoAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.Condo)
            throw new InvalidOperationException($"Cannot set condo detail for property type '{PropertyType}'");

        CondoDetail = detail;
    }

    /// <summary>
    /// Set the land and building detail for this property
    /// </summary>
    public void SetLandAndBuildingDetail(LandAndBuildingAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.LandAndBuilding)
            throw new InvalidOperationException($"Cannot set land and building detail for property type '{PropertyType}'");

        LandAndBuildingDetail = detail;
    }

    /// <summary>
    /// Set the vehicle detail for this property
    /// </summary>
    public void SetVehicleDetail(VehicleAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.Vehicle)
            throw new InvalidOperationException($"Cannot set vehicle detail for property type '{PropertyType}'");

        VehicleDetail = detail;
    }

    /// <summary>
    /// Set the vessel detail for this property
    /// </summary>
    public void SetVesselDetail(VesselAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.Vessel)
            throw new InvalidOperationException($"Cannot set vessel detail for property type '{PropertyType}'");

        VesselDetail = detail;
    }

    /// <summary>
    /// Set the machinery detail for this property
    /// </summary>
    public void SetMachineryDetail(MachineryAppraisalDetail detail)
    {
        if (PropertyType != PropertyType.Machinery)
            throw new InvalidOperationException($"Cannot set machinery detail for property type '{PropertyType}'");

        MachineryDetail = detail;
    }

    #endregion
}