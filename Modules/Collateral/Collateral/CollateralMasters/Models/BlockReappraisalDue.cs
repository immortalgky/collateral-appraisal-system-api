namespace Collateral.CollateralMasters.Models;

/// <summary>
/// Materialized read model produced by <see cref="Services.BlockReappraisalDueScanJob"/>.
/// One row per block-project CollateralMaster that is due for reappraisal.
/// Phase C (screen) reads from this table; Phase D marks rows as Consumed when a
/// reappraisal request is raised.
/// </summary>
public class BlockReappraisalDue
{
    public Guid Id { get; private set; }

    /// <summary>FK / unique key — one pending row per project CollateralMaster.</summary>
    public Guid CollateralMasterId { get; private set; }

    public string? ProjectName { get; private set; }
    public string ProjectType { get; private set; } = null!;
    public string? OldAppraisalNumber { get; private set; }
    public decimal? ProjectSellingPrice { get; private set; }
    public int TotalUnits { get; private set; }
    public int RemainingUnits { get; private set; }
    public DateTime? LastAppraisedDate { get; private set; }
    public DateTime DueDate { get; private set; }

    /// <summary>"Pending" or "Consumed". Default: "Pending".</summary>
    public string Status { get; private set; } = "Pending";

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private BlockReappraisalDue() { }

    public static BlockReappraisalDue Create(
        Guid collateralMasterId,
        string? projectName,
        string projectType,
        string? oldAppraisalNumber,
        decimal? projectSellingPrice,
        int totalUnits,
        int remainingUnits,
        DateTime? lastAppraisedDate,
        DateTime dueDate)
    {
        var now = DateTime.UtcNow;
        return new BlockReappraisalDue
        {
            Id = Guid.CreateVersion7(),
            CollateralMasterId = collateralMasterId,
            ProjectName = projectName,
            ProjectType = projectType,
            OldAppraisalNumber = oldAppraisalNumber,
            ProjectSellingPrice = projectSellingPrice,
            TotalUnits = totalUnits,
            RemainingUnits = remainingUnits,
            LastAppraisedDate = lastAppraisedDate,
            DueDate = dueDate,
            Status = "Pending",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Refreshes snapshot fields without changing Status or CreatedAt.
    /// Called by the scan job when the project row already exists as Pending.
    /// </summary>
    public void UpdateSnapshot(
        string? projectName,
        string projectType,
        string? oldAppraisalNumber,
        decimal? projectSellingPrice,
        int totalUnits,
        int remainingUnits,
        DateTime? lastAppraisedDate,
        DateTime dueDate)
    {
        ProjectName = projectName;
        ProjectType = projectType;
        OldAppraisalNumber = oldAppraisalNumber;
        ProjectSellingPrice = projectSellingPrice;
        TotalUnits = totalUnits;
        RemainingUnits = remainingUnits;
        LastAppraisedDate = lastAppraisedDate;
        DueDate = dueDate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks this row as Consumed — called by Phase D when a reappraisal is raised.
    /// </summary>
    public void MarkConsumed()
    {
        Status = "Consumed";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Re-opens a previously Consumed row when the project becomes due again on a later scan cycle.
    /// Called after UpdateSnapshot so both snapshot fields and Status are refreshed atomically.
    /// </summary>
    public void Reactivate()
    {
        Status = "Pending";
        UpdatedAt = DateTime.UtcNow;
    }
}
