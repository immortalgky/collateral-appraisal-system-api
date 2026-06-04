namespace Collateral.CollateralMasters.Models;

/// <summary>
/// 1:1 detail row for a PRJ (block-project) CollateralMaster.
/// Mirrors CondoDetail conventions: private EF ctor, internal domain ctor,
/// UpdateStructure mutator, owned AppraisalSummary, synced IsDeleted.
///
/// StructureJson stores a serialized snapshot of units/towers/models at the time
/// of last appraisal completion — the Collateral module never needs to parse it
/// back; it is opaque storage for audit / reappraisal prefill by the FE.
/// </summary>
public class ProjectDetail
{
    public Guid CollateralMasterId { get; private set; }

    // Project-level fields (last-known)
    public string ProjectType { get; private set; } = null!; // "Condo" | "LandAndBuilding"
    public string? ProjectName { get; private set; }
    public string? Developer { get; private set; }
    public string? Address { get; private set; }
    public string? Province { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int TotalUnits { get; private set; }
    public int RemainingUnits { get; private set; }
    public decimal? ProjectSellingPrice { get; private set; }

    // Opaque JSON snapshot of unit/tower/model list at last appraisal
    public string StructureJson { get; private set; } = "{}";

    // Appraisal summary (owned VO)
    public AppraisalSummary AppraisalSummary { get; private set; } = null!;

    // Synced from CollateralMaster for filtered-index support
    public bool IsDeleted { get; private set; }

    private ProjectDetail() { }

    internal ProjectDetail(Guid collateralMasterId, string projectType, string? projectName, bool isDeleted)
    {
        CollateralMasterId = collateralMasterId;
        ProjectType = projectType;
        ProjectName = projectName;
        IsDeleted = isDeleted;
        AppraisalSummary = new AppraisalSummary(null, null, null);
    }

    /// <summary>
    /// Overwrites all last-known fields with data from the latest appraisal.
    /// </summary>
    public void UpdateStructure(
        string projectType,
        string? projectName,
        string? developer,
        string? address,
        string? province,
        decimal? latitude,
        decimal? longitude,
        int totalUnits,
        int remainingUnits,
        decimal? projectSellingPrice,
        string structureJson)
    {
        ProjectType = projectType;
        ProjectName = projectName;
        Developer = developer;
        Address = address;
        Province = province;
        Latitude = latitude;
        Longitude = longitude;
        TotalUnits = totalUnits;
        RemainingUnits = remainingUnits;
        ProjectSellingPrice = projectSellingPrice;
        StructureJson = structureJson;
    }

    /// <summary>Stamps the appraisal summary owned VO.</summary>
    public void UpdateAppraisalSummary(Guid appraisalId, string appraisalNumber, DateTime appraisedDate)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate);
    }

    internal void SetIsDeleted(bool isDeleted) => IsDeleted = isDeleted;
}
