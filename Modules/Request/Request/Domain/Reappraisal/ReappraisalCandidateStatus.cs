namespace Request.Domain.Reappraisal;

/// <summary>
/// Lifecycle status of a staged reappraisal candidate row.
/// </summary>
public enum ReappraisalCandidateStatus
{
    /// <summary>Ingested and waiting for staff action.</summary>
    Pending,

    /// <summary>Staff initiated reappraisal requests from this candidate.</summary>
    Consumed,

    /// <summary>Staff soft-deleted this candidate from the list.</summary>
    Deleted
}
