namespace Collateral.CollateralMasters.Reappraisal;

/// <summary>
/// Lifecycle status of a staged reappraisal candidate row.
/// </summary>
public enum ReappraisalCandidateStatus
{
    /// <summary>Ingested and waiting for staff action.</summary>
    Pending,

    /// <summary>The reappraisal request for this book has been submitted.</summary>
    Consumed,

    /// <summary>
    /// "Not reviewing this round" (UI label). Kept under its original name so no stored value has to
    /// change; stays until staff restore it, and is still refreshed by later files.
    /// </summary>
    Deleted
}
