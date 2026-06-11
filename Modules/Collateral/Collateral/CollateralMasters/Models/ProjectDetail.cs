namespace Collateral.CollateralMasters.Models;

/// <summary>
/// 1:1 detail row for a PRJ (block-project) CollateralMaster.
/// Mirrors CondoDetail conventions: private EF ctor, internal domain ctor,
/// UpdateStructure mutator, owned AppraisalSummary, synced IsDeleted.
///
/// Phase 1: <c>StructureJson</c> has been replaced by a first-class
/// <see cref="ProjectUnit"/> child collection (see <c>collateral.ProjectUnits</c> table).
/// The opaque JSON blob is no longer stored here.
/// </summary>
public class ProjectDetail
{
    private readonly List<ProjectUnit> _units = [];

    public Guid CollateralMasterId { get; private set; }

    // Project-level fields (last-known)
    public string ProjectType { get; private set; } = null!; // code: "U" (Condo) | "LB" (LandAndBuilding) | "L" (Land)
    public string? ProjectName { get; private set; }
    public string? Developer { get; private set; }
    public string? Address { get; private set; }
    public string? Province { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int TotalUnits { get; private set; }
    public int RemainingUnits { get; private set; }
    public decimal? ProjectSellingPrice { get; private set; }

    /// <summary>
    /// First-class per-unit records. Populated by Phase 2 upsert from appraisal data.
    /// Phase 1: collection exists in schema; no rows are inserted yet.
    /// </summary>
    public IReadOnlyList<ProjectUnit> Units => _units.AsReadOnly();

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
    /// Overwrites all last-known scalar fields with data from the latest appraisal.
    /// <c>StructureJson</c> has been removed — unit records are managed via
    /// <see cref="ReplaceUnits"/> instead (Phase 2).
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
        decimal? projectSellingPrice)
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
    }

    /// <summary>
    /// Replaces the unit collection wholesale from a new appraisal snapshot.
    /// Clears existing units then adds all incoming units.
    /// Phase 2 upsert calls this after building the <see cref="ProjectUnit"/> list from appraisal data.
    /// </summary>
    public void ReplaceUnits(IEnumerable<ProjectUnit> units)
    {
        _units.Clear();
        _units.AddRange(units);
    }

    /// <summary>
    /// Recalculates <see cref="RemainingUnits"/> as the count of units where
    /// <see cref="ProjectUnit.IsSold"/> is false.
    /// Call after any mutation that changes sale status (e.g. block-reappraisal sold-mark).
    /// </summary>
    public void RecountRemaining()
    {
        RemainingUnits = _units.Count(u => !u.IsSold);
    }

    /// <summary>Stamps the appraisal summary owned VO.</summary>
    public void UpdateAppraisalSummary(Guid appraisalId, string appraisalNumber, DateTime appraisedDate)
    {
        AppraisalSummary.Update(appraisalId, appraisalNumber, appraisedDate);
    }

    internal void SetIsDeleted(bool isDeleted) => IsDeleted = isDeleted;
}
